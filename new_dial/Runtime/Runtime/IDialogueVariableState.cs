// Copyright (c) 2026 Danil Kashulin. All rights reserved.

namespace NewDial.DialogueEditor
{
    public interface IDialogueVariableState : IDialogueVariableStore
    {
        bool TryGetValue(string key, out DialogueArgumentValue value);

        bool TryGetDefinition(string key, out DialogueVariableDefinition definition);

        bool SetValue(string key, DialogueArgumentValue value);

        bool TrySetValueFromString(string key, string value, out string error);
    }
}
