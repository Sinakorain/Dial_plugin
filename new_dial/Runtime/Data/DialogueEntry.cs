using System;

namespace NewDial.DialogueEditor
{
    [Serializable]
    public class DialogueEntry
    {
        public string Id = GuidUtility.NewGuid();
        public string Name = "Dialogue";
        public ConditionData StartCondition = new();
        public DialogueGraphData Graph = new();

        public DialogueEntry Clone()
        {
            return new DialogueEntry
            {
                Id = Id,
                Name = Name,
                StartCondition = StartCondition?.Clone() ?? new ConditionData(),
                Graph = Graph?.Clone() ?? new DialogueGraphData()
            };
        }
    }
}
