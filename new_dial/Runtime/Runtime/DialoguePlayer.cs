using System;
using System.Collections.Generic;

namespace NewDial.DialogueEditor
{
    public class DialoguePlayer
    {
        private readonly IDialogueConditionEvaluator _conditionEvaluator;
        private readonly IDialogueVariableStore _variableStore;
        private readonly List<DialogueChoice> _choices = new();

        private DialogueEntry _currentDialogue;

        public DialoguePlayer(IDialogueConditionEvaluator conditionEvaluator = null, IDialogueVariableStore variableStore = null)
        {
            _conditionEvaluator = conditionEvaluator ?? new DefaultDialogueConditionEvaluator();
            _variableStore = variableStore;
        }

        public event Action<DialogueTextNodeData> NodeChanged;
        public event Action DialogueEnded;

        public DialogueEntry CurrentDialogue => _currentDialogue;

        public DialogueTextNodeData CurrentNode { get; private set; }

        public IReadOnlyList<DialogueChoice> CurrentChoices => _choices;

        public bool CanStart(DialogueEntry dialogue)
        {
            return dialogue != null && _conditionEvaluator.Evaluate(dialogue.StartCondition, _variableStore);
        }

        public bool Start(DialogueEntry dialogue)
        {
            _currentDialogue = dialogue;
            _choices.Clear();

            if (dialogue == null || dialogue.Graph == null)
            {
                CurrentNode = null;
                return false;
            }

            var startNode = DialogueGraphUtility.FindStartNode(dialogue);
            if (startNode == null)
            {
                CurrentNode = null;
                return false;
            }

            SetCurrentNode(startNode);
            return true;
        }

        public bool Next()
        {
            if (CurrentNode == null)
            {
                return false;
            }

            if (CurrentNode.UseOutputsAsChoices && _choices.Count > 0)
            {
                return false;
            }

            foreach (var link in DialogueGraphUtility.GetOutgoingLinks(_currentDialogue.Graph, CurrentNode.Id))
            {
                var target = DialogueGraphUtility.GetTextNode(_currentDialogue.Graph, link.ToNodeId);
                if (target != null && CanEnterNode(target))
                {
                    SetCurrentNode(target);
                    return true;
                }
            }

            EndDialogue();
            return false;
        }

        public bool Choose(int index)
        {
            if (CurrentNode == null || !CurrentNode.UseOutputsAsChoices || index < 0 || index >= _choices.Count)
            {
                return false;
            }

            var choice = _choices[index];
            if (choice.Target == null)
            {
                EndDialogue();
                return false;
            }

            SetCurrentNode(choice.Target);
            return true;
        }

        private bool CanEnterNode(DialogueTextNodeData node)
        {
            return _conditionEvaluator.Evaluate(node?.Condition, _variableStore);
        }

        private void SetCurrentNode(DialogueTextNodeData node)
        {
            CurrentNode = node;
            RebuildChoices();
            NodeChanged?.Invoke(CurrentNode);
        }

        private void RebuildChoices()
        {
            _choices.Clear();
            if (CurrentNode == null || !CurrentNode.UseOutputsAsChoices)
            {
                return;
            }

            foreach (var link in DialogueGraphUtility.GetOutgoingLinks(_currentDialogue.Graph, CurrentNode.Id))
            {
                var target = DialogueGraphUtility.GetTextNode(_currentDialogue.Graph, link.ToNodeId);
                if (target != null && CanEnterNode(target))
                {
                    _choices.Add(new DialogueChoice(link, target));
                }
            }
        }

        private void EndDialogue()
        {
            CurrentNode = null;
            _choices.Clear();
            DialogueEnded?.Invoke();
        }
    }
}
