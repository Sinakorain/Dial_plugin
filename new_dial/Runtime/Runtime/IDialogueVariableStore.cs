namespace NewDial.DialogueEditor
{
    public interface IDialogueVariableStore
    {
        bool TryGetValue(string key, out string value);
    }
}
