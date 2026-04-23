namespace NewDial.DialogueEditor
{
    public interface IDialogueConditionEvaluator
    {
        bool Evaluate(ConditionData condition, IDialogueVariableStore variableStore);
    }
}
