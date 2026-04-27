// Copyright (c) 2026 Danil Kashulin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NewDial.DialogueEditor
{
    public static class DialogueTextLocalizationUtility
    {
        public const string DefaultLanguageCode = "ru";

        public static string GetBodyText(BaseNodeData node, string languageCode)
        {
            return node switch
            {
                DialogueTextNodeData textNode => GetBodyText(textNode, languageCode),
                DialogueChoiceNodeData choiceNode => GetBodyText(choiceNode, languageCode),
                _ => string.Empty
            };
        }

        public static string GetBodyText(DialogueTextNodeData node, string languageCode)
        {
            if (node == null)
            {
                return string.Empty;
            }

            var normalized = NormalizeLanguageCode(languageCode);
            if (!string.IsNullOrWhiteSpace(normalized) &&
                TryGetLocalizedBodyText(node, normalized, out var localizedText))
            {
                return localizedText;
            }

            return node.BodyText ?? string.Empty;
        }

        public static string GetBodyText(DialogueChoiceNodeData node, string languageCode)
        {
            if (node == null)
            {
                return string.Empty;
            }

            var normalized = NormalizeLanguageCode(languageCode);
            if (!string.IsNullOrWhiteSpace(normalized) &&
                TryGetLocalizedBodyText(node, normalized, out var localizedText))
            {
                return localizedText;
            }

            return node.BodyText ?? string.Empty;
        }

        public static void SetBodyText(BaseNodeData node, string languageCode, string text)
        {
            switch (node)
            {
                case DialogueTextNodeData textNode:
                    SetBodyText(textNode, languageCode, text);
                    break;
                case DialogueChoiceNodeData choiceNode:
                    SetBodyText(choiceNode, languageCode, text);
                    break;
            }
        }

        public static void SetBodyText(DialogueTextNodeData node, string languageCode, string text)
        {
            if (node == null)
            {
                return;
            }

            var normalized = NormalizeLanguageCode(languageCode);
            if (string.IsNullOrWhiteSpace(normalized) || normalized == DefaultLanguageCode)
            {
                node.BodyText = text ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            node.LocalizedBodyText ??= new List<DialogueLocalizedTextEntry>();
            var entry = node.LocalizedBodyText.FirstOrDefault(item =>
                item != null && string.Equals(NormalizeLanguageCode(item.LanguageCode), normalized, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                entry = new DialogueLocalizedTextEntry { LanguageCode = normalized };
                node.LocalizedBodyText.Add(entry);
            }

            entry.LanguageCode = normalized;
            entry.Text = text ?? string.Empty;
        }

        public static void SetBodyText(DialogueChoiceNodeData node, string languageCode, string text)
        {
            if (node == null)
            {
                return;
            }

            var normalized = NormalizeLanguageCode(languageCode);
            if (string.IsNullOrWhiteSpace(normalized) || normalized == DefaultLanguageCode)
            {
                node.BodyText = text ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            node.LocalizedBodyText ??= new List<DialogueLocalizedTextEntry>();
            var entry = node.LocalizedBodyText.FirstOrDefault(item =>
                item != null && string.Equals(NormalizeLanguageCode(item.LanguageCode), normalized, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                entry = new DialogueLocalizedTextEntry { LanguageCode = normalized };
                node.LocalizedBodyText.Add(entry);
            }

            entry.LanguageCode = normalized;
            entry.Text = text ?? string.Empty;
        }

        public static bool TryGetLocalizedBodyText(BaseNodeData node, string languageCode, out string text)
        {
            return node switch
            {
                DialogueTextNodeData textNode => TryGetLocalizedBodyText(textNode, languageCode, out text),
                DialogueChoiceNodeData choiceNode => TryGetLocalizedBodyText(choiceNode, languageCode, out text),
                _ => TryReturnEmpty(out text)
            };
        }

        public static bool TryGetLocalizedBodyText(DialogueTextNodeData node, string languageCode, out string text)
        {
            text = string.Empty;
            var normalized = NormalizeLanguageCode(languageCode);
            if (node?.LocalizedBodyText == null || string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            foreach (var entry in node.LocalizedBodyText)
            {
                if (entry == null ||
                    !string.Equals(NormalizeLanguageCode(entry.LanguageCode), normalized, StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrEmpty(entry.Text))
                {
                    continue;
                }

                text = entry.Text;
                return true;
            }

            return false;
        }

        public static bool TryGetLocalizedBodyText(DialogueChoiceNodeData node, string languageCode, out string text)
        {
            text = string.Empty;
            var normalized = NormalizeLanguageCode(languageCode);
            if (node?.LocalizedBodyText == null || string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            foreach (var entry in node.LocalizedBodyText)
            {
                if (entry == null ||
                    !string.Equals(NormalizeLanguageCode(entry.LanguageCode), normalized, StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrEmpty(entry.Text))
                {
                    continue;
                }

                text = entry.Text;
                return true;
            }

            return false;
        }

        public static string NormalizeLanguageCode(string languageCode)
        {
            return (languageCode ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static bool TryReturnEmpty(out string text)
        {
            text = string.Empty;
            return false;
        }
    }
}
