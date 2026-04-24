using System;

namespace NewDial.DialogueEditor
{
    [Serializable]
    public class DialogueLocalizedTextEntry
    {
        public string LanguageCode = string.Empty;
        public string Text = string.Empty;

        public DialogueLocalizedTextEntry Clone()
        {
            return new DialogueLocalizedTextEntry
            {
                LanguageCode = LanguageCode,
                Text = Text
            };
        }
    }
}
