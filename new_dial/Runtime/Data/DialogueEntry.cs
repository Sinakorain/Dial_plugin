using System;
using System.Collections.Generic;
using System.Linq;

namespace NewDial.DialogueEditor
{
    [Serializable]
    public class DialogueEntry
    {
        public string Id = GuidUtility.NewGuid();
        public string Name = "Dialogue";
        public List<DialogueSpeakerEntry> Speakers = new();
        public ConditionData StartCondition = new();
        public DialogueGraphData Graph = new();

        public DialogueEntry Clone()
        {
            return new DialogueEntry
            {
                Id = Id,
                Name = Name,
                Speakers = Speakers?.Where(speaker => speaker != null).Select(speaker => speaker.Clone()).ToList() ?? new List<DialogueSpeakerEntry>(),
                StartCondition = StartCondition?.Clone() ?? new ConditionData(),
                Graph = Graph?.Clone() ?? new DialogueGraphData()
            };
        }
    }
}
