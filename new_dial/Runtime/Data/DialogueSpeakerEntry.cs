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
