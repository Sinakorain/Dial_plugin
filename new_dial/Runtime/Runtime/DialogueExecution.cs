// Copyright (c) 2026 Danil Kashulin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NewDial.DialogueEditor
{
    public enum DialogueExecutionStatus
    {
        Success,
        Failed,
        Pending,
        EndDialogue
    }

    public sealed class DialogueExecutionContext
    {
        public DialogueExecutionContext(
            DialogueEntry dialogue,
            DialogueGraphData graph,
            BaseNodeData node,
            IDialogueVariableStore variableStore,
            IDialogueVariableState variableState = null)
        {
            Dialogue = dialogue;
            Graph = graph;
            Node = node;
            VariableStore = variableStore;
            VariableState = variableState ?? variableStore as IDialogueVariableState;
        }

        public DialogueEntry Dialogue { get; }

        public DialogueGraphData Graph { get; }

        public BaseNodeData Node { get; }

        public IDialogueVariableStore VariableStore { get; }

        public IDialogueVariableState VariableState { get; }
    }

    public static class DialogueBuiltInFunctions
    {
        public const string SetVariableFunctionId = "newdial.variable.set";
        public const string VariableKeyArgument = "Key";
        public const string VariableValueArgument = "Value";

        public static DialogueFunctionDescriptor SetVariableDescriptor => new(
            SetVariableFunctionId,
            "Set Variable",
            "Variables",
            "Sets a dialogue variable in the current runtime session.",
            new[]
            {
                new DialogueParameterDescriptor(VariableKeyArgument, DialogueArgumentType.String, required: true),
                new DialogueParameterDescriptor(VariableValueArgument, DialogueArgumentType.String, required: true)
            },
            defaultFailurePolicy: DialogueExecutionFailurePolicy.StopDialogue);
    }

    public sealed class DialogueExecutionResult
    {
        private DialogueExecutionResult(DialogueExecutionStatus status, string message)
        {
            Status = status;
            Message = message ?? string.Empty;
        }

        public DialogueExecutionStatus Status { get; }

        public string Message { get; }

        public bool IsSuccess => Status == DialogueExecutionStatus.Success;

        public bool IsFailure => Status == DialogueExecutionStatus.Failed;

        public static DialogueExecutionResult Success(string message = null)
        {
            return new DialogueExecutionResult(DialogueExecutionStatus.Success, message);
        }

        public static DialogueExecutionResult Failed(string message)
        {
            return new DialogueExecutionResult(DialogueExecutionStatus.Failed, message);
        }

        public static DialogueExecutionResult Pending(string message = null)
        {
            return new DialogueExecutionResult(DialogueExecutionStatus.Pending, message);
        }

        public static DialogueExecutionResult EndDialogue(string message = null)
        {
            return new DialogueExecutionResult(DialogueExecutionStatus.EndDialogue, message);
        }
    }

    public readonly struct DialogueParameterDescriptor
    {
        public DialogueParameterDescriptor(
            string name,
            DialogueArgumentType type,
            bool required = false,
            DialogueArgumentValue defaultValue = null,
            string hint = null)
        {
            Name = name ?? string.Empty;
            Type = type;
            Required = required;
            DefaultValue = defaultValue?.Clone();
            Hint = hint ?? string.Empty;
        }

        public string Name { get; }

        public DialogueArgumentType Type { get; }

        public bool Required { get; }

        public DialogueArgumentValue DefaultValue { get; }

        public string Hint { get; }
    }

    public readonly struct DialogueFunctionDescriptor
    {
        public DialogueFunctionDescriptor(
            string id,
            string displayName = null,
            string category = null,
            string description = null,
            IEnumerable<DialogueParameterDescriptor> parameters = null,
            bool supportsWaitForCompletion = false,
            DialogueExecutionFailurePolicy defaultFailurePolicy = DialogueExecutionFailurePolicy.StopDialogue)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? id ?? string.Empty;
            Category = category ?? string.Empty;
            Description = description ?? string.Empty;
            Parameters = (parameters ?? Enumerable.Empty<DialogueParameterDescriptor>()).ToList();
            SupportsWaitForCompletion = supportsWaitForCompletion;
            DefaultFailurePolicy = defaultFailurePolicy;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public string Category { get; }

        public string Description { get; }

        public IReadOnlyList<DialogueParameterDescriptor> Parameters { get; }

        public bool SupportsWaitForCompletion { get; }

        public DialogueExecutionFailurePolicy DefaultFailurePolicy { get; }
    }

    public readonly struct DialogueSceneDescriptor
    {
        public DialogueSceneDescriptor(
            string sceneKey,
            string displayName = null,
            string category = null,
            string description = null,
            IEnumerable<DialogueParameterDescriptor> parameters = null,
            bool supportsWaitForCompletion = false)
        {
            SceneKey = sceneKey ?? string.Empty;
            DisplayName = displayName ?? sceneKey ?? string.Empty;
            Category = category ?? string.Empty;
            Description = description ?? string.Empty;
            Parameters = (parameters ?? Enumerable.Empty<DialogueParameterDescriptor>()).ToList();
            SupportsWaitForCompletion = supportsWaitForCompletion;
        }

        public string SceneKey { get; }

        public string DisplayName { get; }

        public string Category { get; }

        public string Description { get; }

        public IReadOnlyList<DialogueParameterDescriptor> Parameters { get; }

        public bool SupportsWaitForCompletion { get; }
    }

    public interface IDialogueExecutionRegistry
    {
        IEnumerable<DialogueFunctionDescriptor> GetFunctions();

        IEnumerable<DialogueSceneDescriptor> GetScenes();
    }

    public interface IDialogueFunctionExecutor
    {
        DialogueExecutionResult Execute(FunctionNodeData node, DialogueExecutionContext context);
    }

    public interface IDialogueSceneExecutor
    {
        DialogueExecutionResult Execute(SceneNodeData node, DialogueExecutionContext context);
    }

    public static class DialogueExecutionRegistry
    {
        private static readonly List<IDialogueExecutionRegistry> Registries = new();

        public static void Register(IDialogueExecutionRegistry registry)
        {
            if (registry != null && !Registries.Contains(registry))
            {
                Registries.Add(registry);
            }
        }

        public static void Unregister(IDialogueExecutionRegistry registry)
        {
            if (registry != null)
            {
                Registries.Remove(registry);
            }
        }

        public static IReadOnlyList<DialogueFunctionDescriptor> GetFunctions()
        {
            return new[] { DialogueBuiltInFunctions.SetVariableDescriptor }
                .Concat(Registries
                .SelectMany(registry => registry.GetFunctions() ?? Enumerable.Empty<DialogueFunctionDescriptor>())
                .Where(descriptor => !string.IsNullOrWhiteSpace(descriptor.Id)))
                .GroupBy(descriptor => descriptor.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(descriptor => descriptor.Category)
                .ThenBy(descriptor => descriptor.DisplayName)
                .ToList();
        }

        public static IReadOnlyList<DialogueSceneDescriptor> GetScenes()
        {
            return Registries
                .SelectMany(registry => registry.GetScenes() ?? Enumerable.Empty<DialogueSceneDescriptor>())
                .Where(descriptor => !string.IsNullOrWhiteSpace(descriptor.SceneKey))
                .GroupBy(descriptor => descriptor.SceneKey, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(descriptor => descriptor.Category)
                .ThenBy(descriptor => descriptor.DisplayName)
                .ToList();
        }

        public static bool TryGetFunction(string id, out DialogueFunctionDescriptor descriptor)
        {
            descriptor = GetFunctions().FirstOrDefault(candidate => candidate.Id == id);
            return !string.IsNullOrWhiteSpace(descriptor.Id);
        }

        public static bool TryGetScene(string sceneKey, out DialogueSceneDescriptor descriptor)
        {
            descriptor = GetScenes().FirstOrDefault(candidate => candidate.SceneKey == sceneKey);
            return !string.IsNullOrWhiteSpace(descriptor.SceneKey);
        }
    }
}
