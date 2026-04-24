using UnityEngine;
using UnityEngine.UIElements;

namespace NewDial.DialogueEditor
{
    internal static class DialogueRichTextRenderer
    {
        private const string RendererClass = "dialogue-rich-text-renderer";
        private const string RunClass = "dialogue-rich-text-run";
        private const string HighlightRunClass = "dialogue-rich-text-run--highlight";
        private const string EmptyRunClass = "dialogue-rich-text-run--empty";

        public static VisualElement Create(string name, string className)
        {
            var container = new VisualElement
            {
                name = name
            };
            container.AddToClassList(RendererClass);
            if (!string.IsNullOrWhiteSpace(className))
            {
                container.AddToClassList(className);
            }

            return container;
        }

        public static void SetText(
            VisualElement container,
            string text,
            string emptyText = "",
            int maxVisibleCharacters = int.MaxValue)
        {
            if (container == null)
            {
                return;
            }

            container.Clear();
            var renderText = string.IsNullOrWhiteSpace(text) ? emptyText : text;
            var runs = DialogueRichTextUtility.ParseSupportedRichText(renderText, maxVisibleCharacters);
            if (runs.Count == 0)
            {
                AddRun(container, new DialogueRichTextRun(string.Empty, false, false, string.Empty, string.Empty), true);
                return;
            }

            foreach (var run in runs)
            {
                AddRun(container, run, false);
            }
        }

        private static void AddRun(VisualElement container, DialogueRichTextRun run, bool isEmpty)
        {
            var label = new Label(run.Text)
            {
                pickingMode = PickingMode.Ignore
            };
            label.AddToClassList(RunClass);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.flexShrink = 1f;
            label.style.maxWidth = Length.Percent(100f);
            if (isEmpty)
            {
                label.AddToClassList(EmptyRunClass);
            }

            if (run.Bold && run.Italic)
            {
                label.style.unityFontStyleAndWeight = FontStyle.BoldAndItalic;
            }
            else if (run.Bold)
            {
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            else if (run.Italic)
            {
                label.style.unityFontStyleAndWeight = FontStyle.Italic;
            }

            if (!string.IsNullOrWhiteSpace(run.TextColor) && ColorUtility.TryParseHtmlString(run.TextColor, out var textColor))
            {
                label.style.color = textColor;
            }

            if (!string.IsNullOrWhiteSpace(run.HighlightColor) && ColorUtility.TryParseHtmlString(run.HighlightColor, out var highlightColor))
            {
                label.style.backgroundColor = highlightColor;
                label.AddToClassList(HighlightRunClass);
            }

            container.Add(label);
        }
    }
}
