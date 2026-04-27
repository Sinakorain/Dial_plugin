// Copyright (c) 2026 Danil Kashulin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NewDial.DialogueEditor
{
    public enum DialogueExecutableValidationSeverity
    {
        Warning,
        Error
    }

    public readonly struct DialogueExecutableValidationIssue
    {
        public DialogueExecutableValidationIssue(DialogueExecutableValidationSeverity severity, string message)
        {
            Severity = severity;
            Message = message ?? string.Empty;
        }

        public DialogueExecutableValidationSeverity Severity { get; }

        public string Message { get; }
    }

    public static class DialogueExecutableValidator
    {
        public static IReadOnlyList<DialogueExecutableValidationIssue> ValidateNode(
            BaseNodeData node,
            IDialogueExecutionRegistry registry = null,
            bool unknownReferencesAreErrors = false)
        {
            var issues = new List<DialogueExecutableValidationIssue>();
            switch (node)
            {
                case FunctionNodeData functionNode:
                    ValidateFunctionNode(issues, functionNode, registry, unknownReferencesAreErrors);
                    break;
                case SceneNodeData sceneNode:
                    ValidateSceneNode(issues, sceneNode, registry, unknownReferencesAreErrors);
                    break;
                case DebugNodeData debugNode:
                    ValidateArgumentList(issues, debugNode.Arguments, blockSaving: false);
                    break;
            }

            return issues;
        }

        private static void ValidateFunctionNode(
            ICollection<DialogueExecutableValidationIssue> issues,
            FunctionNodeData node,
            IDialogueExecutionRegistry registry,
            bool unknownReferencesAreErrors)
        {
            if (string.IsNullOrWhiteSpace(node.FunctionId))
            {
                AddError(issues, "FunctionId is empty.");
            }

            ValidateArgumentList(issues, node.Arguments, blockSaving: true);

            var functions = registry?.GetFunctions()?.ToList() ?? DialogueExecutionRegistry.GetFunctions().ToList();
            if (functions.Count == 0 || string.IsNullOrWhiteSpace(node.FunctionId))
            {
                return;
            }

            if (functions.All(function => function.Id == DialogueBuiltInFunctions.SetVariableFunctionId) &&
                node.FunctionId != DialogueBuiltInFunctions.SetVariableFunctionId)
            {
                return;
            }

            var descriptor = functions.FirstOrDefault(function => function.Id == node.FunctionId);
            if (string.IsNullOrWhiteSpace(descriptor.Id))
            {
                Add(issues, unknownReferencesAreErrors ? DialogueExecutableValidationSeverity.Error : DialogueExecutableValidationSeverity.Warning,
                    $"Function '{node.FunctionId}' is not registered.");
                return;
            }

            ValidateParameters(issues, node.Arguments, descriptor.Parameters);
        }

        private static void ValidateSceneNode(
            ICollection<DialogueExecutableValidationIssue> issues,
            SceneNodeData node,
            IDialogueExecutionRegistry registry,
            bool unknownReferencesAreErrors)
        {
            if (string.IsNullOrWhiteSpace(node.SceneKey))
            {
                AddError(issues, "SceneKey is empty.");
            }

            ValidateArgumentList(issues, node.Parameters, blockSaving: true);

            var scenes = registry?.GetScenes()?.ToList() ?? DialogueExecutionRegistry.GetScenes().ToList();
            if (scenes.Count == 0 || string.IsNullOrWhiteSpace(node.SceneKey))
            {
                return;
            }

            var descriptor = scenes.FirstOrDefault(scene => scene.SceneKey == node.SceneKey);
            if (string.IsNullOrWhiteSpace(descriptor.SceneKey))
            {
                Add(issues, unknownReferencesAreErrors ? DialogueExecutableValidationSeverity.Error : DialogueExecutableValidationSeverity.Warning,
                    $"Scene '{node.SceneKey}' is not registered.");
                return;
            }

            ValidateParameters(issues, node.Parameters, descriptor.Parameters);
        }

        private static void ValidateArgumentList(
            ICollection<DialogueExecutableValidationIssue> issues,
            IEnumerable<DialogueArgumentEntry> arguments,
            bool blockSaving)
        {
            if (arguments == null)
            {
                return;
            }

            foreach (var argument in arguments)
            {
                if (argument == null || argument.Value == null)
                {
                    Add(issues, blockSaving ? DialogueExecutableValidationSeverity.Error : DialogueExecutableValidationSeverity.Warning,
                        "Argument has invalid serialized value data.");
                }
            }
        }

        private static void ValidateParameters(
            ICollection<DialogueExecutableValidationIssue> issues,
            IReadOnlyList<DialogueArgumentEntry> arguments,
            IReadOnlyList<DialogueParameterDescriptor> parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                return;
            }

            var lookup = (arguments ?? Array.Empty<DialogueArgumentEntry>())
                .Where(argument => argument != null && !string.IsNullOrWhiteSpace(argument.Name))
                .GroupBy(argument => argument.Name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            foreach (var parameter in parameters)
            {
                if (!lookup.TryGetValue(parameter.Name, out var argument) || argument.Value == null)
                {
                    if (parameter.Required)
                    {
                        AddError(issues, $"Required argument '{parameter.Name}' is missing.");
                    }

                    continue;
                }

                if (!argument.Value.TypeMatches(parameter.Type))
                {
                    AddError(issues, $"Argument '{parameter.Name}' expects {parameter.Type} but is {argument.Value.Type}.");
                }
            }
        }

        private static void AddError(ICollection<DialogueExecutableValidationIssue> issues, string message)
        {
            Add(issues, DialogueExecutableValidationSeverity.Error, message);
        }

        private static void Add(
            ICollection<DialogueExecutableValidationIssue> issues,
            DialogueExecutableValidationSeverity severity,
            string message)
        {
            issues.Add(new DialogueExecutableValidationIssue(severity, message));
        }
    }
}
