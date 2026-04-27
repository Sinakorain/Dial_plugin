// Copyright (c) 2026 Danil Kashulin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NewDial.DialogueEditor
{
    [Serializable]
    public class DialogueTextNodeData : BaseNodeData
    {
        public string BodyText = string.Empty;
        public string LocalizationKey = string.Empty;
        public List<DialogueLocalizedTextEntry> LocalizedBodyText = new();
        public string VoiceKey = string.Empty;
        public string SpeakerId = string.Empty;
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
                LocalizationKey = LocalizationKey,
                LocalizedBodyText = LocalizedBodyText?.Where(entry => entry != null).Select(entry => entry.Clone()).ToList() ?? new List<DialogueLocalizedTextEntry>(),
                VoiceKey = VoiceKey,
                SpeakerId = SpeakerId,
                IsStartNode = IsStartNode,
                UseOutputsAsChoices = UseOutputsAsChoices
            };
        }
    }
}
