using System.Collections.Generic;

namespace NewDial.DialogueEditor
{
    internal sealed class DialoguePreviewSession
    {
        private readonly List<DialoguePreviewAction> _actions = new();
        private readonly List<DialoguePreviewTranscriptEntry> _transcript = new();
        private readonly DialoguePlayer _player = new();

        public DialoguePreviewSession(DialogueEntry dialogue)
        {
            Dialogue = dialogue;
            Replay();
        }

        public DialogueEntry Dialogue { get; }

        public DialogueTextNodeData CurrentNode => _player.CurrentNode;

        public IReadOnlyList<DialogueChoice> CurrentChoices => _player.CurrentChoices;

        public IReadOnlyList<DialoguePreviewTranscriptEntry> Transcript => _transcript;

        public bool CanAdvance => CurrentNode != null && !CurrentNode.UseOutputsAsChoices;

        public bool CanChoose => CurrentNode != null && CurrentNode.UseOutputsAsChoices && CurrentChoices.Count > 0;

        public bool CanGoBack => _actions.Count > 0;

        public bool IsEnded => Dialogue != null && CurrentNode == null;

        public string CurrentNodeId => CurrentNode?.Id;

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
            _player.Start(Dialogue);

            if (_player.CurrentNode != null)
            {
                _transcript.Add(DialoguePreviewTranscriptEntry.Node(_player.CurrentNode));
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
                        _player.Next();
                        if (_player.CurrentNode != null)
                        {
                            _transcript.Add(DialoguePreviewTranscriptEntry.Node(_player.CurrentNode));
                        }
                        break;
                    case DialoguePreviewActionKind.Choose:
                        if (action.ChoiceIndex < 0 || action.ChoiceIndex >= _player.CurrentChoices.Count)
                        {
                            continue;
                        }

                        var choice = _player.CurrentChoices[action.ChoiceIndex];
                        _transcript.Add(DialoguePreviewTranscriptEntry.Choice(choice.Text));
                        _player.Choose(action.ChoiceIndex);
                        if (_player.CurrentNode != null)
                        {
                            _transcript.Add(DialoguePreviewTranscriptEntry.Node(_player.CurrentNode));
                        }
                        break;
                }
            }
        }
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

        public static DialoguePreviewTranscriptEntry Choice(string choiceText)
        {
            return new DialoguePreviewTranscriptEntry(
                DialoguePreviewTranscriptEntryKind.Choice,
                "Choice",
                string.IsNullOrWhiteSpace(choiceText) ? "Continue" : choiceText,
                null);
        }
    }
}
