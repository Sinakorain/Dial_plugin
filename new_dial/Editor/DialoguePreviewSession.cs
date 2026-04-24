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

        public IReadOnlyList<DialogueChoice> CurrentChoices => _player.CurrentChoices;

        public IReadOnlyList<DialoguePreviewBlockedChoice> BlockedChoices => _blockedChoices;

        public IReadOnlyList<DialoguePreviewTranscriptEntry> Transcript => _transcript;

        public bool CanAdvance => CurrentNode != null && !CurrentNode.UseOutputsAsChoices;

        public bool CanChoose => CurrentNode != null && CurrentNode.UseOutputsAsChoices && CurrentChoices.Count > 0;

        public bool CanGoBack => _actions.Count > 0;

        public bool IsEnded => Dialogue != null && CurrentNode == null && _transcript.Count > 0;

        public string CurrentNodeId => CurrentNode?.Id;

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
                CurrentReason = "No dialogue is selected.";
                return;
            }

            if (Dialogue.Graph == null)
            {
                CurrentReason = "Missing graph.";
                return;
            }

            if (!_conditionEvaluator.Evaluate(Dialogue.StartCondition, variableStore))
            {
                CurrentReason = $"Dialogue start blocked by condition: {DescribeCondition(Dialogue.StartCondition)}";
                return;
            }

            if (FindStartNode(Dialogue) == null)
            {
                CurrentReason = "Missing start node.";
                return;
            }

            _player.Start(Dialogue);

            if (_player.CurrentNode != null)
            {
                _transcript.Add(DialoguePreviewTranscriptEntry.Node(_player.CurrentNode));
            }
            else
            {
                CurrentReason = "Missing valid start node.";
                return;
            }

            foreach (var action in _actions)
            {
                if (_player.CurrentNode == null)
                {
                    break;
                }

                switch (action.Kind)
                {
                    case DialoguePreviewActionKind.Advance:
                        var advanceReason = PredictAdvanceEndReason(_player.CurrentNode, variableStore);
                        _player.Next();
                        if (_player.CurrentNode != null)
                        {
                            _transcript.Add(DialoguePreviewTranscriptEntry.Node(_player.CurrentNode));
                        }
                        else
                        {
                            CurrentReason = advanceReason;
                        }
                        break;
                    case DialoguePreviewActionKind.Choose:
                        if (action.ChoiceIndex < 0 || action.ChoiceIndex >= _player.CurrentChoices.Count)
                        {
                            CurrentReason = "Choice is no longer available with the current test variables.";
                            break;
                        }

                        var choice = _player.CurrentChoices[action.ChoiceIndex];
                        _player.Choose(action.ChoiceIndex);
                        _transcript.Add(DialoguePreviewTranscriptEntry.Choice(choice.Text, _player.CurrentNode));
                        break;
                }
            }

            RebuildBlockedChoices(variableStore);
            if (_player.CurrentNode != null &&
                _player.CurrentNode.UseOutputsAsChoices &&
                _player.CurrentChoices.Count == 0 &&
                _blockedChoices.Count > 0)
            {
                CurrentReason = "No choices are available with the current test variables.";
            }
        }

        public string GetChoiceExplanation(DialogueChoice choice)
        {
            if (choice.Link != null &&
                string.IsNullOrWhiteSpace(choice.Link.ChoiceText) &&
                string.IsNullOrWhiteSpace(choice.Target?.Title))
            {
                return "Generic fallback label is being used.";
            }

            return string.Empty;
        }

        private void RebuildBlockedChoices(IDialogueVariableStore variableStore)
        {
            _blockedChoices.Clear();
            if (_player.CurrentNode == null || !_player.CurrentNode.UseOutputsAsChoices)
            {
                return;
            }

            foreach (var link in GetOutgoingLinks(Dialogue.Graph, _player.CurrentNode.Id))
            {
                var target = GetTextNode(Dialogue.Graph, link.ToNodeId);
                if (target == null)
                {
                    _blockedChoices.Add(new DialoguePreviewBlockedChoice(GetChoiceLabel(link, target), "Missing valid target node."));
                    continue;
                }

                if (!_conditionEvaluator.Evaluate(target.Condition, variableStore))
                {
                    _blockedChoices.Add(new DialoguePreviewBlockedChoice(
                        GetChoiceLabel(link, target),
                        $"Choice unavailable because condition is not met: {DescribeCondition(target.Condition)}"));
                }
            }
        }

        private string PredictAdvanceEndReason(DialogueTextNodeData node, IDialogueVariableStore variableStore)
        {
            var links = GetOutgoingLinks(Dialogue.Graph, node?.Id);
            if (links.Count == 0)
            {
                return "Branch reached an end.";
            }

            var hasBlockedCondition = false;
            var hasBrokenLink = false;
            foreach (var link in links)
            {
                var target = GetTextNode(Dialogue.Graph, link.ToNodeId);
                if (target == null)
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
                return "Broken link or missing valid target node.";
            }

            return hasBlockedCondition
                ? "All outgoing branches are blocked by conditions."
                : "Branch reached an end.";
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

        private static DialogueTextNodeData GetTextNode(DialogueGraphData graph, string nodeId)
        {
            if (graph?.Nodes == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return null;
            }

            return graph.Nodes.FirstOrDefault(node => node != null && node.Id == nodeId) as DialogueTextNodeData;
        }

        private static string GetChoiceLabel(NodeLinkData link, DialogueTextNodeData target)
        {
            if (!string.IsNullOrWhiteSpace(link?.ChoiceText))
            {
                return link.ChoiceText;
            }

            if (!string.IsNullOrWhiteSpace(target?.Title))
            {
                return target.Title;
            }

            return "Choice";
        }

        private static string DescribeCondition(ConditionData condition)
        {
            if (condition == null || condition.Type == ConditionType.None)
            {
                return "None";
            }

            if (condition.Type == ConditionType.Custom)
            {
                return "Custom condition cannot be simulated by the built-in preview.";
            }

            var comparisonOperator = string.IsNullOrWhiteSpace(condition.Operator) ? "==" : condition.Operator.Trim();
            if (comparisonOperator == "Truthy")
            {
                return $"{condition.Type} {condition.Key} is truthy";
            }

            return $"{condition.Type} {condition.Key} {comparisonOperator} {condition.Value}";
        }
    }

    internal readonly struct DialoguePreviewBlockedChoice
    {
        public DialoguePreviewBlockedChoice(string label, string reason)
        {
            Label = label ?? "Choice";
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
        private DialoguePreviewTranscriptEntry(DialoguePreviewTranscriptEntryKind kind, string title, string body, string nodeId)
        {
            Kind = kind;
            Title = title;
            Body = body;
            NodeId = nodeId;
        }

        public DialoguePreviewTranscriptEntryKind Kind { get; }

        public string Title { get; }

        public string Body { get; }

        public string NodeId { get; }

        public static DialoguePreviewTranscriptEntry Node(DialogueTextNodeData node)
        {
            return new DialoguePreviewTranscriptEntry(
                DialoguePreviewTranscriptEntryKind.Node,
                string.IsNullOrWhiteSpace(node?.Title) ? "Untitled" : node.Title,
                node?.BodyText ?? string.Empty,
                node?.Id);
        }

        public static DialoguePreviewTranscriptEntry Choice(string choiceText, DialogueTextNodeData node)
        {
            return new DialoguePreviewTranscriptEntry(
                DialoguePreviewTranscriptEntryKind.Choice,
                string.IsNullOrWhiteSpace(choiceText) ? "Continue" : choiceText,
                string.IsNullOrWhiteSpace(node?.BodyText) ? "This choice has no dialogue text yet." : node.BodyText,
                node?.Id);
        }
    }
}
