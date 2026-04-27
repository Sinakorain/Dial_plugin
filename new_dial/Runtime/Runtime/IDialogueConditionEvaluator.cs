// Copyright (c) 2026 Danil Kashulin. All rights reserved.

namespace NewDial.DialogueEditor
{
    public interface IDialogueConditionEvaluator
    {
        bool Evaluate(ConditionData condition, IDialogueVariableStore variableStore);
    }
}
