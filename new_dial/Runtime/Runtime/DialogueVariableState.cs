// Copyright (c) 2026 Danil Kashulin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NewDial.DialogueEditor
{
    public class DialogueVariableState : IDialogueVariableState
    {
        private readonly Dictionary<string, DialogueVariableDefinition> _definitions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DialogueArgumentValue> _values = new(StringComparer.Ordinal);

        public DialogueVariableState(IEnumerable<DialogueVariableDefinition> definitions = null)
        {
            foreach (var definition in definitions ?? Enumerable.Empty<DialogueVariableDefinition>())
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Key))
                {
                    continue;
                }

                var clone = definition.Clone();
                clone.DefaultValue ??= new DialogueArgumentValue { Type = clone.Type };
                clone.DefaultValue.Type = clone.Type;
                _definitions[clone.Key] = clone;
                _values[clone.Key] = clone.DefaultValue.Clone();
            }
        }

        public static DialogueVariableState FromDatabase(DialogueDatabaseAsset database)
        {
            return new DialogueVariableState(database?.Variables);
        }

        bool IDialogueVariableStore.TryGetValue(string key, out string value)
        {
            if (TryGetValue(key, out var typedValue))
            {
                value = typedValue.GetDisplayValue();
                return true;
            }

            value = string.Empty;
            return false;
        }

        public bool TryGetValue(string key, out DialogueArgumentValue value)
        {
            if (!string.IsNullOrWhiteSpace(key) && _values.TryGetValue(key, out var currentValue))
            {
                value = currentValue?.Clone() ?? new DialogueArgumentValue();
                return true;
            }

            value = null;
            return false;
        }

        public bool TryGetDefinition(string key, out DialogueVariableDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(key) && _definitions.TryGetValue(key, out var currentDefinition))
            {
                definition = currentDefinition?.Clone();
                return definition != null;
            }

            definition = null;
            return false;
        }

        public bool SetValue(string key, DialogueArgumentValue value)
        {
            if (string.IsNullOrWhiteSpace(key) ||
                value == null ||
                !_definitions.TryGetValue(key, out var definition) ||
                value.Type != definition.Type)
            {
                return false;
            }

            _values[key] = value.Clone();
            return true;
        }

        public void SetStringValue(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            _values[key] = DialogueArgumentValue.FromString(value ?? string.Empty);
        }

        public bool TrySetValueFromString(string key, string value, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(key))
            {
                error = "Variable key is empty.";
                return false;
            }

            if (!_definitions.TryGetValue(key, out var definition))
            {
                error = $"Variable '{key}' is not defined.";
                return false;
            }

            if (!TryParseValue(value, definition.Type, out var parsedValue))
            {
                error = $"Variable '{key}' expects {definition.Type} value.";
                return false;
            }

            _values[key] = parsedValue;
            return true;
        }

        public static bool TryParseValue(string value, DialogueArgumentType type, out DialogueArgumentValue parsedValue)
        {
            value ??= string.Empty;
            switch (type)
            {
                case DialogueArgumentType.Int:
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                    {
                        parsedValue = DialogueArgumentValue.FromInt(intValue);
                        return true;
                    }

                    break;
                case DialogueArgumentType.Float:
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
                    {
                        parsedValue = DialogueArgumentValue.FromFloat(floatValue);
                        return true;
                    }

                    break;
                case DialogueArgumentType.Bool:
                    if (TryParseBool(value, out var boolValue))
                    {
                        parsedValue = DialogueArgumentValue.FromBool(boolValue);
                        return true;
                    }

                    break;
                default:
                    parsedValue = DialogueArgumentValue.FromString(value);
                    return true;
            }

            parsedValue = null;
            return false;
        }

        private static bool TryParseBool(string value, out bool boolValue)
        {
            if (bool.TryParse(value, out boolValue))
            {
                return true;
            }

            if (string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "1", StringComparison.OrdinalIgnoreCase))
            {
                boolValue = true;
                return true;
            }

            if (string.Equals(value, "no", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "0", StringComparison.OrdinalIgnoreCase))
            {
                boolValue = false;
                return true;
            }

            return false;
        }
    }
}
