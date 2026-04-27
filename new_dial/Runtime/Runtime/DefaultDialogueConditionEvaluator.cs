// Copyright (c) 2026 Danil Kashulin. All rights reserved.

using System;
using System.Globalization;

namespace NewDial.DialogueEditor
{
    public class DefaultDialogueConditionEvaluator : IDialogueConditionEvaluator
    {
        private const double NumericTolerance = 0.000001d;

        public bool Evaluate(ConditionData condition, IDialogueVariableStore variableStore)
        {
            if (condition == null || condition.Type == ConditionType.None)
            {
                return true;
            }

            if (condition.Type == ConditionType.Custom)
            {
                return false;
            }

            if (variableStore == null || string.IsNullOrWhiteSpace(condition.Key))
            {
                return false;
            }

            var comparisonOperator = string.IsNullOrWhiteSpace(condition.Operator) ? "==" : condition.Operator.Trim();
            var expectedValue = condition.Value ?? string.Empty;

            if (variableStore is IDialogueVariableState variableState &&
                variableState.TryGetValue(condition.Key, out var typedValue))
            {
                return Compare(typedValue, comparisonOperator, expectedValue);
            }

            if (!variableStore.TryGetValue(condition.Key, out var currentValue))
            {
                return false;
            }

            return Compare(currentValue ?? string.Empty, comparisonOperator, expectedValue);
        }

        private static bool Compare(DialogueArgumentValue currentValue, string comparisonOperator, string expectedValue)
        {
            if (currentValue == null)
            {
                return false;
            }

            switch (currentValue.Type)
            {
                case DialogueArgumentType.Int:
                    return CompareNumber(currentValue.IntValue, comparisonOperator, expectedValue);
                case DialogueArgumentType.Float:
                    return CompareNumber(currentValue.FloatValue, comparisonOperator, expectedValue);
                case DialogueArgumentType.Bool:
                    return CompareBool(currentValue.BoolValue, comparisonOperator, expectedValue);
                default:
                    return Compare(currentValue.StringValue ?? string.Empty, comparisonOperator, expectedValue);
            }
        }

        private static bool Compare(string currentValue, string comparisonOperator, string expectedValue)
        {
            switch (comparisonOperator)
            {
                case "==":
                    return string.Equals(currentValue, expectedValue, StringComparison.OrdinalIgnoreCase);
                case "!=":
                    return !string.Equals(currentValue, expectedValue, StringComparison.OrdinalIgnoreCase);
                case "Contains":
                    return currentValue.IndexOf(expectedValue, StringComparison.OrdinalIgnoreCase) >= 0;
                case "Truthy":
                    return IsTruthy(currentValue);
            }

            if (TryParseNumber(currentValue, out var left) && TryParseNumber(expectedValue, out var right))
            {
                return comparisonOperator switch
                {
                    ">" => left > right,
                    "<" => left < right,
                    ">=" => left >= right,
                    "<=" => left <= right,
                    _ => false
                };
            }

            return false;
        }

        private static bool CompareNumber(double currentValue, string comparisonOperator, string expectedValue)
        {
            if (comparisonOperator == "Truthy")
            {
                return Math.Abs(currentValue) >= NumericTolerance;
            }

            if (!TryParseNumber(expectedValue, out var expectedNumber))
            {
                return false;
            }

            return comparisonOperator switch
            {
                "==" => Math.Abs(currentValue - expectedNumber) < NumericTolerance,
                "!=" => Math.Abs(currentValue - expectedNumber) >= NumericTolerance,
                ">" => currentValue > expectedNumber,
                "<" => currentValue < expectedNumber,
                ">=" => currentValue >= expectedNumber,
                "<=" => currentValue <= expectedNumber,
                _ => false
            };
        }

        private static bool CompareBool(bool currentValue, string comparisonOperator, string expectedValue)
        {
            if (comparisonOperator == "Truthy")
            {
                return currentValue;
            }

            if (string.IsNullOrWhiteSpace(expectedValue) &&
                (comparisonOperator == "==" || comparisonOperator == "!="))
            {
                expectedValue = "true";
            }

            if (!TryParseBool(expectedValue, out var expectedBool))
            {
                return false;
            }

            return comparisonOperator switch
            {
                "==" => currentValue == expectedBool,
                "!=" => currentValue != expectedBool,
                _ => false
            };
        }

        private static bool TryParseNumber(string value, out double number)
        {
            return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out number);
        }

        private static bool IsTruthy(string value)
        {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
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
