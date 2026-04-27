// Copyright (c) 2026 Danil Kashulin. All rights reserved.

namespace NewDial.DialogueEditor
{
    public interface IDialogueVariableStore
    {
        bool TryGetValue(string key, out string value);
    }
}
