using System;
using System.Collections.Generic;
using System.Linq;

namespace NewDial.DialogueEditor
{
    internal sealed class DialoguePreviewSession
    {
        private readonly List<DialoguePreviewAction> _actions = new();
        private readonly List<DialoguePreviewTranscriptEntry> _transcript = new();
        private readonly List<DialoguePreviewBlockedChoice> _blockedChoices = new();
        private readonly Dictionary<string, string> _variables = new();
        private readonly IDialogueConditionEvaluator _conditionEvaluator = new DefaultDialogueConditionEvaluator();
        private DialoguePlayer _player;

        public DialoguePreviewSession(DialogueEntry dialogue, IDictionary<string, string> variables = null)
        {
            Dialogue = dialogue;
            SetVariables(variables, false);
            Replay();
        }

        public DialogueEntry Dialogue { get; }

        public DialogueTextNodeData CurrentNode => _player.CurrentNode;

        public BaseNodeData CurrentLineNode => _player.CurrentLineNode;

        public IReadOnlyList<DialogueChoice> CurrentChoices => _player.CurrentChoices;

        public IReadOnlyList<DialoguePreviewBlockedChoice> BlockedChoices => _blockedChoices;

        public IReadOnlyList<DialoguePreviewTranscriptEntry> Transcript => _transcript;

        public bool CanAdvance => CurrentLineNode != null &&
                                  (CurrentLineNode is not DialogueTextNodeData textNode ||
                                   !DialogueGraphUtility.UsesChoices(Dialogue?.Graph, textNode));

        public bool CanChoose => CurrentLineNode is DialogueTextNodeData textNode &&
                                 DialogueGraphUtility.UsesChoices(Dialogue?.Graph, textNode) &&
                                 CurrentChoices.Count > 0;

        public bool CanGoBack => _actions.Count > 0;

        public bool IsEnded => Dialogue != null && CurrentLineNode == null && _transcript.Count > 0;

        public string CurrentNodeId => CurrentLineNode?.Id;

        public string CurrentSpeakerName => _player.CurrentSpeakerName;

        public string CurrentReason { get; private set; }

        public void SetVariables(IDictionary<string, string> variables, bool replay = true)
        {
            _variables.Clear();
            if (variables != null)
            {
                foreach (var pair in variables.Where(pair => !string.IsNullOrWhiteSpace(pair.Key)))
                {
                    _variables[pair.Key] = pair.Value ?? string.Empty;
                }
            }

            if (replay)
            {
                Replay();
            }
        }

        public void Restart()
        {
            _actions.Clear();
            Replay();
        }

        public bool Advance()
        {
            if (!CanAdvance)
            {
                return false;
            }

            _actions.Add(DialoguePreviewAction.Advance());
            Replay();
            return true;
        }

        public bool Choose(int choiceIndex)
        {
            if (!CanChoose || choiceIndex < 0 || choiceIndex >= CurrentChoices.Count)
            {
                return false;
            }

            _actions.Add(DialoguePreviewAction.Choose(choiceIndex));
            Replay();
            return true;
        }

        public bool Back()
        {
            if (_actions.Count == 0)
            {
                return false;
            }

            _actions.RemoveAt(_actions.Count - 1);
            Replay();
            return true;
        }

        private void Replay()
        {
            _transcript.Clear();
            _blockedChoices.Clear();
            CurrentReason = string.Empty;

            var variableStore = new DictionaryDialogueVariableStore(_variables);
            _player = new DialoguePlayer(_conditionEvaluator, variableStore);

            if (Dialogue == null)
            {
                CurrentReason = DialogueEditorLocalization.Text("No dialogue is selected.");
                return;
            }

            if (Dialogue.Graph == null)
            {
                CurrentReason = DialogueEditorLocalization.Text("Missing graph.");
                return;
            }

            if (!_conditionEvaluator.Evaluate(Dialogue.StartCondition, variableStore))
            {
                CurrentReason = DialogueEditorLocalization.Format("Dialogue start blocked by condition: {0}", DescribeCondition(Dialogue.StartCondition));
                return;
            }

            if (FindStartNode(Dialogue) == null)
            {
                CurrentReason = DialogueEditorLocalization.Text("Missing start node.");
                return;
            }

            _player.Start(Dialogue);

            if (_player.CurrentLineNode != null)
            {
                _transcript.Add(DialoguePreviewTranscriptEntry.Node(_player.CurrentLineNode, _player.CurrentSpeakerName));
            }
            else
            {
                CurrentReason = DialogueEditorLocalization.Text("Missing valid start node.");
                return;
            }

            foreach (var action in _actions)
            {
                if (_player.CurrentLineNode == null)
                {
                    break;
                }

                switch (action.Kind)
                {
                    case DialoguePreviewActionKind.Advance:
                        var advanceReason = PredictAdvanceEndReason(_player.CurrentLineNode, variableStore);
                        _player.Next();
                        if (_player.CurrentLineNode != null)
                        {
                            _transcript.Add(DialoguePreviewTranscriptEntry.Node(_player.CurrentLineNode, _player.CurrentSpeakerName));
                        }
                        else
                        {
                            CurrentReason = advanceReason;
                        }
                        break;
                    case DialoguePreviewActionKind.Choose:
                        if (action.ChoiceIndex < 0 || action.ChoiceIndex >= _player.CurrentChoices.Count)
                        {
                            CurrentReason = DialogueEditorLocalization.Text("Choice is no longer available with the current test variables.");
                            break;
                        }

                        var choice = _player.CurrentChoices[action.ChoiceIndex];
                        _player.Choose(action.ChoiceIndex);
                        _transcript.Add(DialoguePreviewTranscriptEntry.Choice(choice.Text, _player.CurrentLineNode, _player.CurrentSpeakerName));
                        break;
                }
            }

            RebuildBlockedChoices(variableStore);
            if (_player.CurrentLineNode is DialogueTextNodeData currentTextNode &&
                DialogueGraphUtility.UsesChoices(Dialogue.Graph, currentTextNode) &&
                _player.CurrentChoices.Count == 0 &&
                _blockedChoices.Count > 0)
            {
                CurrentReason = DialogueEditorLocalization.Text("No choices are available with the current test variables.");
            }
        }

        public string GetChoiceExplanation(DialogueChoice choice)
        {
            if (choice.Link != null &&
                string.IsNullOrWhiteSpace(choice.ChoiceNode?.ChoiceText) &&
                string.IsNullOrWhiteSpace(choice.Link.ChoiceText) &&
                string.IsNullOrWhiteSpace(choice.TargetNode?.Title))
            {
                return DialogueEditorLocalization.Text("Generic fallback label is being used.");
            }

            return string.Empty;
        }

        private void RebuildBlockedChoices(IDialogueVariableStore variableStore)
        {
            _blockedChoices.Clear();
            if (_player.CurrentLineNode is not DialogueTextNodeData currentTextNode ||
                !DialogueGraphUtility.UsesChoices(Dialogue.Graph, currentTextNode))
            {
                return;
            }

            foreach (var link in DialogueGraphUtility.GetChoiceCandidateLinks(Dialogue.Graph, currentTextNode))
            {
                var target = GetNode(Dialogue.Graph, link.ToNodeId);
                if (target == null || !DialogueGraphUtility.IsRuntimeNode(target))
                {
                    _blockedChoices.Add(new DialoguePreviewBlockedChoice(GetChoiceLabel(link, target), DialogueEditorLocalization.Text("Missing valid target node.")));
                    continue;
                }

                if (target is DialogueChoiceNodeData choiceNode)
                {
                    var label = GetChoiceLabel(link, choiceNode);
                    if (!_conditionEvaluator.Evaluate(choiceNode.Condition, variableStore))
                    {
                        _blockedChoices.Add(new DialoguePreviewBlockedChoice(
                            label,
                            DialogueEditorLocalization.Format("Choice unavailable because condition is not met: {0}", DescribeCondition(choiceNode.Condition))));
                        continue;
                    }

                    continue;
                }

                if (!_conditionEvaluator.Evaluate(target.Condition, variableStore))
                {
                    _blockedChoices.Add(new DialoguePreviewBlockedChoice(
                        GetChoiceLabel(link, target),
                        DialogueEditorLocalization.Format("Choice unavailable because condition is not met: {0}", DescribeCondition(target.Condition))));
                }
            }
        }

        private string PredictAdvanceEndReason(BaseNodeData node, IDialogueVariableStore variableStore)
        {
            var links = GetOutgoingLinks(Dialogue.Graph, node?.Id);
            if (links.Count == 0)
            {
                return DialogueEditorLocalization.Text("Branch reached an end.");
            }

            var hasBlockedCondition = false;
            var hasBrokenLink = false;
            foreach (var link in links)
            {
                var target = GetNode(Dialogue.Graph, link.ToNodeId);
                if (target == null || !DialogueGraphUtility.IsRuntimeNode(target))
                {
                    hasBrokenLink = true;
                    continue;
                }

                if (_conditionEvaluator.Evaluate(target.Condition, variableStore))
                {
                    return string.Empty;
                }

                hasBlockedCondition = true;
            }

            if (hasBrokenLink)
            {
                return DialogueEditorLocalization.Text("Broken link or missing valid target node.");
            }

            return hasBlockedCondition
                ? DialogueEditorLocalization.Text("All outgoing branches are blocked by conditions.")
                : DialogueEditorLocalization.Text("Branch reached an end.");
        }

        private static DialogueTextNodeData FindStartNode(DialogueEntry dialogue)
        {
            return dialogue?.Graph?.Nodes?.OfType<DialogueTextNodeData>().FirstOrDefault(node => node.IsStartNode) ??
                   dialogue?.Graph?.Nodes?.OfType<DialogueTextNodeData>().FirstOrDefault();
        }

        private static List<NodeLinkData> GetOutgoingLinks(DialogueGraphData graph, string nodeId)
        {
            if (graph?.Links == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return new List<NodeLinkData>();
            }

            return graph.Links
                .Where(link => link != null && link.FromNodeId == nodeId)
                .OrderBy(link => link.Order)
                .ThenBy(link => link.Id)
                .ToList();
        }

        private static BaseNodeData GetNode(DialogueGraphData graph, string nodeId)
        {
            if (graph?.Nodes == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return null;
            }

            return graph.Nodes.FirstOrDefault(node => node != null && node.Id == nodeId);
        }

        private static string GetChoiceLabel(NodeLinkData link, BaseNodeData target)
        {
            if (target is DialogueChoiceNodeData choiceNode && !string.IsNullOrWhiteSpace(choiceNode.ChoiceText))
            {
                return choiceNode.ChoiceText;
            }

            if (!string.IsNullOrWhiteSpace(link?.ChoiceText))
            {
                return link.ChoiceText;
            }

            if (!string.IsNullOrWhiteSpace(target?.Title))
            {
                return target.Title;
            }

            return DialogueEditorLocalization.Text("Choice");
        }

        private static string DescribeCondition(ConditionData condition)
        {
            if (condition == null || condition.Type == ConditionType.None)
            {
                return DialogueEditorLocalization.Text("None");
            }

            if (condition.Type == ConditionType.Custom)
            {
                return DialogueEditorLocalization.Text("Custom condition cannot be simulated by the built-in preview.");
            }

            var comparisonOperator = string.IsNullOrWhiteSpace(condition.Operator) ? "==" : condition.Operator.Trim();
            if (comparisonOperator == "Truthy")
            {
                return DialogueEditorLocalization.Format("{0} {1} is truthy", condition.Type, condition.Key);
            }

            return $"{condition.Type} {condition.Key} {comparisonOperator} {condition.Value}";
        }
    }

    internal readonly struct DialoguePreviewBlockedChoice
    {
        public DialoguePreviewBlockedChoice(string label, string reason)
        {
            Label = label ?? DialogueEditorLocalization.Text("Choice");
            Reason = reason ?? string.Empty;
        }

        public string Label { get; }

        public string Reason { get; }
    }

    internal enum DialoguePreviewActionKind
    {
        Advance,
        Choose
    }

    internal readonly struct DialoguePreviewAction
    {
        private DialoguePreviewAction(DialoguePreviewActionKind kind, int choiceIndex)
        {
            Kind = kind;
            ChoiceIndex = choiceIndex;
        }

        public DialoguePreviewActionKind Kind { get; }

        public int ChoiceIndex { get; }

        public static DialoguePreviewAction Advance()
        {
            return new DialoguePreviewAction(DialoguePreviewActionKind.Advance, -1);
        }

        public static DialoguePreviewAction Choose(int choiceIndex)
        {
            return new DialoguePreviewAction(DialoguePreviewActionKind.Choose, choiceIndex);
        }
    }

    internal enum DialoguePreviewTranscriptEntryKind
    {
        Node,
        Choice
    }

    internal readonly struct DialoguePreviewTranscriptEntry
    {
        private DialoguePreviewTranscriptEntry(DialoguePreviewTranscriptEntryKind kind, string title, string body, string nodeId, string choiceText = null)
        {
            Kind = kind;
            Title = title;
            Body = body;
            NodeId = nodeId;
            ChoiceText = choiceText ?? string.Empty;
        }

        public DialoguePreviewTranscriptEntryKind Kind { get; }

        public string Title { get; }

        public string Body { get; }

        public string NodeId { get; }

        public string ChoiceText { get; }

        public static DialoguePreviewTranscriptEntry Node(BaseNodeData node, string speakerName)
        {
            return new DialoguePreviewTranscriptEntry(
                DialoguePreviewTranscriptEntryKind.Node,
                string.IsNullOrWhiteSpace(speakerName)
                    ? string.IsNullOrWhiteSpace(node?.Title) ? DialogueEditorLocalization.Text("Untitled") : node.Title
                    : speakerName,
                DialogueTextLocalizationUtility.GetBodyText(node, DialogueContentLanguageSettings.CurrentLanguageCode),
                node?.Id);
        }

        public static DialoguePreviewTranscriptEntry Choice(string choiceText, BaseNodeData node, string speakerName)
        {
            var localizedBody = DialogueTextLocalizationUtility.GetBodyText(node, DialogueContentLanguageSettings.CurrentLanguageCode);
            var body = string.IsNullOrWhiteSpace(localizedBody)
                ? DialogueEditorLocalization.Text("This choice has no dialogue text yet.")
                : localizedBody;

            return new DialoguePreviewTranscriptEntry(
                DialoguePreviewTranscriptEntryKind.Choice,
                string.IsNullOrWhiteSpace(speakerName)
                    ? string.IsNullOrWhiteSpace(choiceText) ? DialogueEditorLocalization.Text("Continue") : choiceText
                    : speakerName,
                body,
                node?.Id,
                choiceText);
        }
    }
}
