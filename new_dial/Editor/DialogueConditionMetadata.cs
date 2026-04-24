using System;
using System.Collections.Generic;
using System.Linq;

namespace NewDial.DialogueEditor
{
    public readonly struct DialogueConditionKeySuggestion
    {
        public DialogueConditionKeySuggestion(string key, string label = null, string category = null)
        {
            Key = key ?? string.Empty;
            Label = label ?? key ?? string.Empty;
            Category = category ?? string.Empty;
        }

        public string Key { get; }

        public string Label { get; }

        public string Category { get; }
    }

    public interface IDialogueConditionMetadataProvider
    {
        IEnumerable<DialogueConditionKeySuggestion> GetKeySuggestions(ConditionType type);
    }

    public static class DialogueConditionMetadataRegistry
    {
        private static readonly List<IDialogueConditionMetadataProvider> Providers = new();

        public static void RegisterProvider(IDialogueConditionMetadataProvider provider)
        {
            if (provider != null && !Providers.Contains(provider))
            {
                Providers.Add(provider);
            }
        }

        public static void UnregisterProvider(IDialogueConditionMetadataProvider provider)
        {
            if (provider != null)
            {
                Providers.Remove(provider);
            }
        }

        public static IReadOnlyList<DialogueConditionKeySuggestion> GetKeySuggestions(ConditionType type)
        {
            return Providers
                .SelectMany(provider => provider.GetKeySuggestions(type) ?? Enumerable.Empty<DialogueConditionKeySuggestion>())
                .Where(suggestion => !string.IsNullOrWhiteSpace(suggestion.Key))
                .GroupBy(suggestion => suggestion.Key, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(suggestion => suggestion.Category)
                .ThenBy(suggestion => suggestion.Label)
                .ToList();
        }

        internal static DialogueConditionMetadata GetMetadata(ConditionType type)
        {
            return type switch
            {
                ConditionType.None => new DialogueConditionMetadata(
                    type,
                    Array.Empty<string>(),
                    showKey: false,
                    showOperator: false,
                    showValue: false,
                    "No condition is required."),
                ConditionType.Custom => new DialogueConditionMetadata(
                    type,
                    new[] { "==", "!=", "Contains", "Truthy", ">", "<", ">=", "<=" },
                    showKey: true,
                    showOperator: true,
                    showValue: true,
                    "Custom conditions require a project evaluator. The built-in editor preview treats them as unavailable by default."),
                ConditionType.TrustLevel => new DialogueConditionMetadata(
                    type,
                    new[] { "==", "!=", ">", "<", ">=", "<=" },
                    showKey: true,
                    showOperator: true,
                    showValue: true,
                    "Expected value: number, using invariant format such as 10 or 2.5."),
                _ => new DialogueConditionMetadata(
                    type,
                    new[] { "==", "!=", "Contains", "Truthy" },
                    showKey: true,
                    showOperator: true,
                    showValue: true,
                    "Expected value: text, true/false, yes/no, or 1/0 depending on the selected operator.")
            };
        }
    }

    internal readonly struct DialogueConditionMetadata
    {
        public DialogueConditionMetadata(
            ConditionType type,
            IReadOnlyList<string> operators,
            bool showKey,
            bool showOperator,
            bool showValue,
            string valueHint)
        {
            Type = type;
            Operators = operators ?? Array.Empty<string>();
            ShowKey = showKey;
            ShowOperator = showOperator;
            ShowValue = showValue;
            ValueHint = valueHint ?? string.Empty;
        }

        public ConditionType Type { get; }

        public IReadOnlyList<string> Operators { get; }

        public bool ShowKey { get; }

        public bool ShowOperator { get; }

        public bool ShowValue { get; }

        public string ValueHint { get; }

        public bool UsesExpectedValue => ShowValue && !string.Equals(DefaultOperator, "Truthy", StringComparison.Ordinal);

        public string DefaultOperator => Operators.Count > 0 ? Operators[0] : string.Empty;
    }
}
