using System;
using System.Collections.Generic;
using System.Linq;

namespace NewDial.DialogueEditor
{
    [Serializable]
    public class DialogueChoiceNodeData : BaseNodeData
    {
        public string ChoiceText = string.Empty;
        public string BodyText = string.Empty;
        public string LocalizationKey = string.Empty;
        public List<DialogueLocalizedTextEntry> LocalizedBodyText = new();
        public string VoiceKey = string.Empty;
        public string SpeakerId = string.Empty;

        public override BaseNodeData Clone()
        {
            return new DialogueChoiceNodeData
            {
                Id = Id,
                Title = Title,
                Position = Position,
                Condition = Condition?.Clone() ?? new ConditionData(),
                ChoiceText = ChoiceText,
                BodyText = BodyText,
                LocalizationKey = LocalizationKey,
                LocalizedBodyText = LocalizedBodyText?.Where(entry => entry != null).Select(entry => entry.Clone()).ToList() ?? new List<DialogueLocalizedTextEntry>(),
                VoiceKey = VoiceKey,
                SpeakerId = SpeakerId
            };
        }
    }
}
