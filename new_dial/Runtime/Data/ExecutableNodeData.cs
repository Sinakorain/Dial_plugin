using System;
using System.Collections.Generic;
using System.Linq;

namespace NewDial.DialogueEditor
{
    public enum DialogueSceneLoadMode
    {
        Single,
        Additive
    }

    public enum DialogueExecutionFailurePolicy
    {
        StopDialogue,
        LogAndContinue
    }

    public enum DialogueDebugLogLevel
    {
        Info,
        Warning,
        Error
    }

    [Serializable]
    public class SceneNodeData : BaseNodeData
    {
        public string SceneKey = string.Empty;
        public DialogueSceneLoadMode LoadMode = DialogueSceneLoadMode.Single;
        public string EntryPointId = string.Empty;
        public string TransitionId = string.Empty;
        public bool CloseDialogueBeforeExecute = true;
        public bool WaitForCompletion;
        public List<DialogueArgumentEntry> Parameters = new();

        public override BaseNodeData Clone()
        {
            return new SceneNodeData
            {
                Id = Id,
                Title = Title,
                Position = Position,
                Condition = Condition?.Clone() ?? new ConditionData(),
                SceneKey = SceneKey,
                LoadMode = LoadMode,
                EntryPointId = EntryPointId,
                TransitionId = TransitionId,
                CloseDialogueBeforeExecute = CloseDialogueBeforeExecute,
                WaitForCompletion = WaitForCompletion,
                Parameters = Parameters?.Where(argument => argument != null).Select(argument => argument.Clone()).ToList() ?? new List<DialogueArgumentEntry>()
            };
        }
    }

    [Serializable]
    public class FunctionNodeData : BaseNodeData
    {
        public string FunctionId = string.Empty;
        public bool CloseDialogueBeforeExecute;
        public bool WaitForCompletion;
        public DialogueExecutionFailurePolicy FailurePolicy = DialogueExecutionFailurePolicy.StopDialogue;
        public List<DialogueArgumentEntry> Arguments = new();

        public override BaseNodeData Clone()
        {
            return new FunctionNodeData
            {
                Id = Id,
                Title = Title,
                Position = Position,
                Condition = Condition?.Clone() ?? new ConditionData(),
                FunctionId = FunctionId,
                CloseDialogueBeforeExecute = CloseDialogueBeforeExecute,
                WaitForCompletion = WaitForCompletion,
                FailurePolicy = FailurePolicy,
                Arguments = Arguments?.Where(argument => argument != null).Select(argument => argument.Clone()).ToList() ?? new List<DialogueArgumentEntry>()
            };
        }
    }

    [Serializable]
    public class DebugNodeData : BaseNodeData
    {
        public string MessageTemplate = string.Empty;
        public DialogueDebugLogLevel LogLevel = DialogueDebugLogLevel.Info;
        public bool IncludeArguments;
        public DialogueExecutionFailurePolicy FailurePolicy = DialogueExecutionFailurePolicy.LogAndContinue;
        public List<DialogueArgumentEntry> Arguments = new();

        public override BaseNodeData Clone()
        {
            return new DebugNodeData
            {
                Id = Id,
                Title = Title,
                Position = Position,
                Condition = Condition?.Clone() ?? new ConditionData(),
                MessageTemplate = MessageTemplate,
                LogLevel = LogLevel,
                IncludeArguments = IncludeArguments,
                FailurePolicy = FailurePolicy,
                Arguments = Arguments?.Where(argument => argument != null).Select(argument => argument.Clone()).ToList() ?? new List<DialogueArgumentEntry>()
            };
        }
    }
}
