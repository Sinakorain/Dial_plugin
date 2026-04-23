using System;

namespace NewDial.DialogueEditor
{
    [Serializable]
    public class ConditionData
    {
        public ConditionType Type = ConditionType.None;
        public string Key = string.Empty;
        public string Operator = "==";
        public string Value = string.Empty;

        public ConditionData Clone()
        {
            return new ConditionData
            {
                Type = Type,
                Key = Key,
                Operator = Operator,
                Value = Value
            };
        }
    }
}
