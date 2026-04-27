// Copyright (c) 2026 Danil Kashulin. All rights reserved.

using System;
using System.Globalization;

namespace NewDial.DialogueEditor
{
    public enum DialogueArgumentType
    {
        String,
        Int,
        Float,
        Bool
    }

    [Serializable]
    public class DialogueArgumentValue
    {
        public DialogueArgumentType Type;
        public string StringValue = string.Empty;
        public int IntValue;
        public float FloatValue;
        public bool BoolValue;

        public DialogueArgumentValue Clone()
        {
            return new DialogueArgumentValue
            {
                Type = Type,
                StringValue = StringValue,
                IntValue = IntValue,
                FloatValue = FloatValue,
                BoolValue = BoolValue
            };
        }

        public object GetValue()
        {
            return Type switch
            {
                DialogueArgumentType.Int => IntValue,
                DialogueArgumentType.Float => FloatValue,
                DialogueArgumentType.Bool => BoolValue,
                _ => StringValue ?? string.Empty
            };
        }

        public string GetDisplayValue()
        {
            return Type switch
            {
                DialogueArgumentType.Int => IntValue.ToString(CultureInfo.InvariantCulture),
                DialogueArgumentType.Float => FloatValue.ToString(CultureInfo.InvariantCulture),
                DialogueArgumentType.Bool => BoolValue ? "true" : "false",
                _ => StringValue ?? string.Empty
            };
        }

        public bool TypeMatches(DialogueArgumentType expectedType)
        {
            return Type == expectedType;
        }

        public static DialogueArgumentValue FromString(string value)
        {
            return new DialogueArgumentValue
            {
                Type = DialogueArgumentType.String,
                StringValue = value ?? string.Empty
            };
        }

        public static DialogueArgumentValue FromInt(int value)
        {
            return new DialogueArgumentValue
            {
                Type = DialogueArgumentType.Int,
                IntValue = value
            };
        }

        public static DialogueArgumentValue FromFloat(float value)
        {
            return new DialogueArgumentValue
            {
                Type = DialogueArgumentType.Float,
                FloatValue = value
            };
        }

        public static DialogueArgumentValue FromBool(bool value)
        {
            return new DialogueArgumentValue
            {
                Type = DialogueArgumentType.Bool,
                BoolValue = value
            };
        }
    }

    [Serializable]
    public class DialogueArgumentEntry
    {
        public string Name = string.Empty;
        public DialogueArgumentValue Value = new();

        public DialogueArgumentEntry Clone()
        {
            return new DialogueArgumentEntry
            {
                Name = Name,
                Value = Value?.Clone() ?? new DialogueArgumentValue()
            };
        }
    }
}
