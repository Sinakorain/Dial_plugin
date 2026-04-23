using System;

namespace NewDial.DialogueEditor
{
    [Serializable]
    public class DialogueTextNodeData : BaseNodeData
    {
        public string BodyText = string.Empty;
        public bool IsStartNode;
        public bool UseOutputsAsChoices;

        public override BaseNodeData Clone()
        {
            return new DialogueTextNodeData
            {
                Id = Id,
                Title = Title,
                Position = Position,
                Condition = Condition?.Clone() ?? new ConditionData(),
                BodyText = BodyText,
                IsStartNode = IsStartNode,
                UseOutputsAsChoices = UseOutputsAsChoices
            };
        }
    }
}
