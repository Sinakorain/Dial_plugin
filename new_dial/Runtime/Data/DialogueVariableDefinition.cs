using System;

namespace NewDial.DialogueEditor
{
    [Serializable]
    public class DialogueVariableDefinition
    {
        public string Key = string.Empty;
        public string DisplayName = string.Empty;
        public DialogueArgumentType Type = DialogueArgumentType.String;
        public DialogueArgumentValue DefaultValue = new();

        public DialogueVariableDefinition Clone()
        {
            var value = DefaultValue?.Clone() ?? new DialogueArgumentValue { Type = Type };
            value.Type = Type;
            return new DialogueVariableDefinition
            {
                Key = Key,
                DisplayName = DisplayName,
                Type = Type,
                DefaultValue = value
            };
        }
    }
}
