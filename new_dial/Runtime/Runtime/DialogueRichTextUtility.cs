using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace NewDial.DialogueEditor
{
    public enum DialogueRichTextFormatKind
    {
        Bold,
        Italic,
        TextColor,
        Highlight
    }

    public readonly struct DialogueRichTextFormat
    {
        private DialogueRichTextFormat(DialogueRichTextFormatKind kind, string color)
        {
            Kind = kind;
            Color = color ?? string.Empty;
        }

        public DialogueRichTextFormatKind Kind { get; }

        public string Color { get; }

        public string OpeningTag => Kind switch
        {
            DialogueRichTextFormatKind.Bold => "<b>",
            DialogueRichTextFormatKind.Italic => "<i>",
            DialogueRichTextFormatKind.TextColor => $"<color={NormalizeColor(Color, false)}>",
            DialogueRichTextFormatKind.Highlight => $"<mark={NormalizeColor(Color, true)}>",
            _ => string.Empty
        };

        public string ClosingTag => Kind switch
        {
            DialogueRichTextFormatKind.Bold => "</b>",
            DialogueRichTextFormatKind.Italic => "</i>",
            DialogueRichTextFormatKind.TextColor => "</color>",
            DialogueRichTextFormatKind.Highlight => "</mark>",
            _ => string.Empty
        };

        public static DialogueRichTextFormat Bold()
        {
            return new DialogueRichTextFormat(DialogueRichTextFormatKind.Bold, string.Empty);
        }

        public static DialogueRichTextFormat Italic()
        {
            return new DialogueRichTextFormat(DialogueRichTextFormatKind.Italic, string.Empty);
        }

        public static DialogueRichTextFormat TextColor(string color)
        {
            return new DialogueRichTextFormat(DialogueRichTextFormatKind.TextColor, color);
        }

        public static DialogueRichTextFormat Highlight(string color)
        {
            return new DialogueRichTextFormat(DialogueRichTextFormatKind.Highlight, color);
        }

        private static string NormalizeColor(string color, bool allowAlpha)
        {
            var normalized = DialogueRichTextUtility.NormalizeHexColor(color, allowAlpha);
            return string.IsNullOrWhiteSpace(normalized)
                ? allowAlpha ? "#FFE06680" : "#FFFFFF"
                : normalized;
        }
    }

    public readonly struct DialogueRichTextRun
    {
        public DialogueRichTextRun(string text, bool bold, bool italic, string textColor, string highlightColor)
        {
            Text = text ?? string.Empty;
            Bold = bold;
            Italic = italic;
            TextColor = textColor ?? string.Empty;
            HighlightColor = highlightColor ?? string.Empty;
        }

        public string Text { get; }

        public bool Bold { get; }

        public bool Italic { get; }

        public string TextColor { get; }

        public string HighlightColor { get; }
    }

    public static class DialogueRichTextUtility
    {
        private static readonly Regex TextColorTagRegex = new("^color=(#[0-9a-fA-F]{6})$", RegexOptions.Compiled);
        private static readonly Regex HighlightTagRegex = new("^mark=(#[0-9a-fA-F]{6}(?:[0-9a-fA-F]{2})?)$", RegexOptions.Compiled);

        public static string SanitizeSupportedRichText(string text)
        {
            return SanitizeSupportedRichText(text, int.MaxValue);
        }

        public static string SanitizeSupportedRichText(string text, int maxVisibleCharacters)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            var openTags = new Stack<string>();
            var visibleCount = 0;
            var truncated = false;

            for (var index = 0; index < text.Length;)
            {
                if (visibleCount >= maxVisibleCharacters)
                {
                    truncated = index < text.Length;
                    break;
                }

                if (text[index] == '<' && TryReadTag(text, index, out var tagEnd, out var rawTag))
                {
                    if (TryNormalizeSupportedTag(rawTag, out var normalizedTag, out var closingTag, out var isClosing))
                    {
                        if (isClosing)
                        {
                            if (openTags.Count > 0 && openTags.Peek() == closingTag)
                            {
                                builder.Append(normalizedTag);
                                openTags.Pop();
                            }
                            else
                            {
                                AppendEscapedVisibleText(builder, text.Substring(index, tagEnd - index + 1), maxVisibleCharacters, ref visibleCount);
                            }
                        }
                        else
                        {
                            builder.Append(normalizedTag);
                            openTags.Push(closingTag);
                        }
                    }
                    else
                    {
                        AppendEscapedVisibleText(builder, text.Substring(index, tagEnd - index + 1), maxVisibleCharacters, ref visibleCount);
                    }

                    index = tagEnd + 1;
                    continue;
                }

                AppendEscapedVisibleText(builder, text[index].ToString(), maxVisibleCharacters, ref visibleCount);
                index++;
            }

            if (truncated)
            {
                builder.Append("...");
            }

            foreach (var closingTag in openTags)
            {
                builder.Append(closingTag);
            }

            return builder.ToString();
        }

        public static string StripRichText(string text)
        {
            return StripSupportedRichText(text, 0, text?.Length ?? 0);
        }

        public static string StripSupportedRichText(string text, int selectionStart, int selectionEnd)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var start = Clamp(Math.Min(selectionStart, selectionEnd), 0, text.Length);
            var end = Clamp(Math.Max(selectionStart, selectionEnd), 0, text.Length);
            if (start == end)
            {
                start = 0;
                end = text.Length;
            }

            var selectedText = text.Substring(start, end - start);
            var stripped = StripSupportedTagsFromText(selectedText);
            return text.Substring(0, start) + stripped + text.Substring(end);
        }

        public static string WrapSelection(string text, int selectionStart, int selectionEnd, DialogueRichTextFormat format)
        {
            text ??= string.Empty;
            var start = Clamp(Math.Min(selectionStart, selectionEnd), 0, text.Length);
            var end = Clamp(Math.Max(selectionStart, selectionEnd), 0, text.Length);
            return text.Substring(0, start) +
                   format.OpeningTag +
                   text.Substring(start, end - start) +
                   format.ClosingTag +
                   text.Substring(end);
        }

        public static IReadOnlyList<DialogueRichTextRun> ParseSupportedRichText(string text)
        {
            return ParseSupportedRichText(text, int.MaxValue);
        }

        public static IReadOnlyList<DialogueRichTextRun> ParseSupportedRichText(string text, int maxVisibleCharacters)
        {
            var sanitized = SanitizeSupportedRichText(text, maxVisibleCharacters);
            var runs = new List<DialogueRichTextRun>();
            if (string.IsNullOrEmpty(sanitized))
            {
                return runs;
            }

            var textBuilder = new StringBuilder();
            var styleStack = new Stack<DialogueRichTextStyle>();
            var currentStyle = DialogueRichTextStyle.Default;

            for (var index = 0; index < sanitized.Length;)
            {
                if (sanitized[index] == '<' &&
                    TryReadTag(sanitized, index, out var tagEnd, out var rawTag) &&
                    TryParseNormalizedStyleTag(rawTag, currentStyle, out var nextStyle, out var isClosing))
                {
                    AddRichTextRun(runs, textBuilder, currentStyle);
                    if (isClosing)
                    {
                        currentStyle = styleStack.Count > 0 ? styleStack.Pop() : DialogueRichTextStyle.Default;
                    }
                    else
                    {
                        styleStack.Push(currentStyle);
                        currentStyle = nextStyle;
                    }

                    index = tagEnd + 1;
                    continue;
                }

                textBuilder.Append(sanitized[index]);
                index++;
            }

            AddRichTextRun(runs, textBuilder, currentStyle);
            return runs;
        }

        public static bool TryNormalizeTextColorCode(string color, out string normalized)
        {
            return TryNormalizeStrictHexColor(color, false, out normalized);
        }

        public static bool TryNormalizeHighlightColorCode(string color, out string normalized)
        {
            return TryNormalizeStrictHexColor(color, true, out normalized);
        }

        internal static string NormalizeHexColor(string color, bool allowAlpha)
        {
            if (string.IsNullOrWhiteSpace(color))
            {
                return string.Empty;
            }

            var value = color.Trim();
            if (!value.StartsWith("#", StringComparison.Ordinal))
            {
                value = $"#{value}";
            }

            var expectedLength = allowAlpha ? 9 : 7;
            if (value.Length != expectedLength)
            {
                return string.Empty;
            }

            for (var index = 1; index < value.Length; index++)
            {
                if (!Uri.IsHexDigit(value[index]))
                {
                    return string.Empty;
                }
            }

            return value.ToUpperInvariant();
        }

        private static bool TryNormalizeStrictHexColor(string color, bool requireAlpha, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(color))
            {
                return false;
            }

            var value = color.Trim();
            if (!value.StartsWith("#", StringComparison.Ordinal))
            {
                return false;
            }

            var expectedLength = requireAlpha ? 9 : 7;
            if (value.Length != expectedLength)
            {
                return false;
            }

            for (var index = 1; index < value.Length; index++)
            {
                if (!Uri.IsHexDigit(value[index]))
                {
                    return false;
                }
            }

            normalized = value.ToUpperInvariant();
            return true;
        }

        private static void AddRichTextRun(List<DialogueRichTextRun> runs, StringBuilder textBuilder, DialogueRichTextStyle style)
        {
            if (textBuilder.Length == 0)
            {
                return;
            }

            runs.Add(new DialogueRichTextRun(
                DecodeEscapedRichText(textBuilder.ToString()),
                style.Bold,
                style.Italic,
                style.TextColor,
                style.HighlightColor));
            textBuilder.Clear();
        }

        private static string DecodeEscapedRichText(string text)
        {
            return text
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&amp;", "&");
        }

        private static bool TryParseNormalizedStyleTag(
            string rawTag,
            DialogueRichTextStyle currentStyle,
            out DialogueRichTextStyle nextStyle,
            out bool isClosing)
        {
            nextStyle = currentStyle;
            isClosing = false;

            if (string.Equals(rawTag, "b", StringComparison.OrdinalIgnoreCase))
            {
                nextStyle = currentStyle.WithBold(true);
                return true;
            }

            if (string.Equals(rawTag, "i", StringComparison.OrdinalIgnoreCase))
            {
                nextStyle = currentStyle.WithItalic(true);
                return true;
            }

            var colorMatch = TextColorTagRegex.Match(rawTag);
            if (colorMatch.Success)
            {
                nextStyle = currentStyle.WithTextColor(NormalizeHexColor(colorMatch.Groups[1].Value, false));
                return true;
            }

            var highlightMatch = HighlightTagRegex.Match(rawTag);
            if (highlightMatch.Success)
            {
                nextStyle = currentStyle.WithHighlightColor(NormalizeHexColor(
                    highlightMatch.Groups[1].Value,
                    highlightMatch.Groups[1].Value.Length == 9));
                return true;
            }

            if (string.Equals(rawTag, "/b", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawTag, "/i", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawTag, "/color", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawTag, "/mark", StringComparison.OrdinalIgnoreCase))
            {
                isClosing = true;
                return true;
            }

            return false;
        }

        private static string StripSupportedTagsFromText(string text)
        {
            var builder = new StringBuilder();
            for (var index = 0; index < text.Length;)
            {
                if (text[index] == '<' &&
                    TryReadTag(text, index, out var tagEnd, out var rawTag) &&
                    TryNormalizeSupportedTag(rawTag, out _, out _, out _))
                {
                    index = tagEnd + 1;
                    continue;
                }

                builder.Append(text[index]);
                index++;
            }

            return builder.ToString();
        }

        private static void AppendEscapedVisibleText(StringBuilder builder, string text, int maxVisibleCharacters, ref int visibleCount)
        {
            foreach (var character in text)
            {
                if (visibleCount >= maxVisibleCharacters)
                {
                    return;
                }

                builder.Append(EscapeRichTextCharacter(character));
                visibleCount++;
            }
        }

        private static string EscapeRichTextCharacter(char character)
        {
            return character switch
            {
                '<' => "&lt;",
                '>' => "&gt;",
                '&' => "&amp;",
                _ => character.ToString()
            };
        }

        private static bool TryReadTag(string text, int tagStart, out int tagEnd, out string rawTag)
        {
            tagEnd = text.IndexOf('>', tagStart);
            if (tagEnd < 0)
            {
                rawTag = string.Empty;
                return false;
            }

            rawTag = text.Substring(tagStart + 1, tagEnd - tagStart - 1).Trim();
            return true;
        }

        private static bool TryNormalizeSupportedTag(
            string rawTag,
            out string normalizedTag,
            out string closingTag,
            out bool isClosing)
        {
            normalizedTag = string.Empty;
            closingTag = string.Empty;
            isClosing = false;

            if (string.Equals(rawTag, "b", StringComparison.OrdinalIgnoreCase))
            {
                normalizedTag = "<b>";
                closingTag = "</b>";
                return true;
            }

            if (string.Equals(rawTag, "/b", StringComparison.OrdinalIgnoreCase))
            {
                normalizedTag = "</b>";
                closingTag = "</b>";
                isClosing = true;
                return true;
            }

            if (string.Equals(rawTag, "i", StringComparison.OrdinalIgnoreCase))
            {
                normalizedTag = "<i>";
                closingTag = "</i>";
                return true;
            }

            if (string.Equals(rawTag, "/i", StringComparison.OrdinalIgnoreCase))
            {
                normalizedTag = "</i>";
                closingTag = "</i>";
                isClosing = true;
                return true;
            }

            if (string.Equals(rawTag, "/color", StringComparison.OrdinalIgnoreCase))
            {
                normalizedTag = "</color>";
                closingTag = "</color>";
                isClosing = true;
                return true;
            }

            if (string.Equals(rawTag, "/mark", StringComparison.OrdinalIgnoreCase))
            {
                normalizedTag = "</mark>";
                closingTag = "</mark>";
                isClosing = true;
                return true;
            }

            var colorMatch = TextColorTagRegex.Match(rawTag);
            if (colorMatch.Success)
            {
                var color = NormalizeHexColor(colorMatch.Groups[1].Value, false);
                normalizedTag = $"<color={color}>";
                closingTag = "</color>";
                return true;
            }

            var highlightMatch = HighlightTagRegex.Match(rawTag);
            if (highlightMatch.Success)
            {
                var color = NormalizeHexColor(highlightMatch.Groups[1].Value, highlightMatch.Groups[1].Value.Length == 9);
                normalizedTag = $"<mark={color}>";
                closingTag = "</mark>";
                return true;
            }

            return false;
        }

        private static int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private readonly struct DialogueRichTextStyle
        {
            public DialogueRichTextStyle(bool bold, bool italic, string textColor, string highlightColor)
            {
                Bold = bold;
                Italic = italic;
                TextColor = textColor ?? string.Empty;
                HighlightColor = highlightColor ?? string.Empty;
            }

            public static readonly DialogueRichTextStyle Default = new(false, false, string.Empty, string.Empty);

            public bool Bold { get; }

            public bool Italic { get; }

            public string TextColor { get; }

            public string HighlightColor { get; }

            public DialogueRichTextStyle WithBold(bool bold)
            {
                return new DialogueRichTextStyle(bold, Italic, TextColor, HighlightColor);
            }

            public DialogueRichTextStyle WithItalic(bool italic)
            {
                return new DialogueRichTextStyle(Bold, italic, TextColor, HighlightColor);
            }

            public DialogueRichTextStyle WithTextColor(string textColor)
            {
                return new DialogueRichTextStyle(Bold, Italic, textColor, HighlightColor);
            }

            public DialogueRichTextStyle WithHighlightColor(string highlightColor)
            {
                return new DialogueRichTextStyle(Bold, Italic, TextColor, highlightColor);
            }
        }
    }
}
