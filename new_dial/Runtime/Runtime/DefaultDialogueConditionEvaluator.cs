using System;
using System.Globalization;

namespace NewDial.DialogueEditor
{
    public class DefaultDialogueConditionEvaluator : IDialogueConditionEvaluator
    {
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

            if (!variableStore.TryGetValue(condition.Key, out var currentValue))
            {
                return false;
            }

            var comparisonOperator = string.IsNullOrWhiteSpace(condition.Operator) ? "==" : condition.Operator.Trim();
            var expectedValue = condition.Value ?? string.Empty;

            return Compare(currentValue ?? string.Empty, comparisonOperator, expectedValue);
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
    }
}
