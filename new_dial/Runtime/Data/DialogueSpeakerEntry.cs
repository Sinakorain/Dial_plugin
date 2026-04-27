// Copyright (c) 2026 Danil Kashulin. All rights reserved.

using System;

namespace NewDial.DialogueEditor
{
    [Serializable]
    public class DialogueSpeakerEntry
    {
        public string Id = GuidUtility.NewGuid();
        public string Name = "Speaker";

        public DialogueSpeakerEntry Clone()
        {
            return new DialogueSpeakerEntry
            {
                Id = Id,
                Name = Name
            };
        }
    }
}
