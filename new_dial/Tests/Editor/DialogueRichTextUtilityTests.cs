using NUnit.Framework;

namespace NewDial.DialogueEditor.Tests
{
    public class DialogueRichTextUtilityTests
    {
        [Test]
        public void WrapSelection_AddsBoldTagsAroundSelection()
        {
            var result = DialogueRichTextUtility.WrapSelection("Hello world", 6, 11, DialogueRichTextFormat.Bold());

            Assert.That(result, Is.EqualTo("Hello <b>world</b>"));
        }

        [Test]
        public void WrapSelection_InsertsPairedTagsForEmptySelection()
        {
            var result = DialogueRichTextUtility.WrapSelection("Hello", 5, 5, DialogueRichTextFormat.Italic());

            Assert.That(result, Is.EqualTo("Hello<i></i>"));
        }

        [Test]
        public void WrapSelection_NormalizesColorAndHighlightTags()
        {
            var color = DialogueRichTextUtility.WrapSelection("Danger", 0, 6, DialogueRichTextFormat.TextColor("ff6b6b"));
            var highlight = DialogueRichTextUtility.WrapSelection("Clue", 0, 4, DialogueRichTextFormat.Highlight("#ffe06680"));

            Assert.That(color, Is.EqualTo("<color=#FF6B6B>Danger</color>"));
            Assert.That(highlight, Is.EqualTo("<mark=#FFE06680>Clue</mark>"));
        }

        [Test]
        public void StripSupportedRichText_RemovesOnlySupportedTags()
        {
            var text = "<b>Hello</b> <custom>x</custom> <color=#FF0000>red</color>";

            var result = DialogueRichTextUtility.StripSupportedRichText(text, 0, text.Length);

            Assert.That(result, Is.EqualTo("Hello <custom>x</custom> red"));
        }

        [Test]
        public void SanitizeSupportedRichText_PreservesSupportedTagsAndEscapesUnknownTags()
        {
            var text = "Hi <b>there</b> <script>x</script> <color=#ff0000>red</color> <color=bad>bad</color>";

            var result = DialogueRichTextUtility.SanitizeSupportedRichText(text);

            Assert.That(result, Is.EqualTo("Hi <b>there</b> &lt;script&gt;x&lt;/script&gt; <color=#FF0000>red</color> &lt;color=bad&gt;bad&lt;/color&gt;"));
        }

        [Test]
        public void SanitizeSupportedRichText_TruncatesVisibleCharactersAndClosesOpenTags()
        {
            var result = DialogueRichTextUtility.SanitizeSupportedRichText("<b>Hello world</b>", 5);

            Assert.That(result, Is.EqualTo("<b>Hello...</b>"));
        }

        [Test]
        public void SanitizeSupportedRichText_EscapesUnmatchedClosingTags()
        {
            var result = DialogueRichTextUtility.SanitizeSupportedRichText("Hello </b>world");

            Assert.That(result, Is.EqualTo("Hello &lt;/b&gt;world"));
        }

        [Test]
        public void SanitizeSupportedRichText_EscapesMismatchedClosingTags()
        {
            var result = DialogueRichTextUtility.SanitizeSupportedRichText("<b><i>Hello</b></i>");

            Assert.That(result, Is.EqualTo("<b><i>Hello&lt;/b&gt;</i></b>"));
        }

        [Test]
        public void StripRichText_ReturnsPlainTextForSupportedTags()
        {
            var result = DialogueRichTextUtility.StripRichText("<b>Hello</b> <i>friend</i>");

            Assert.That(result, Is.EqualTo("Hello friend"));
        }

        [Test]
        public void ParseSupportedRichText_ReturnsStyledRunsForNestedMarkup()
        {
            var runs = DialogueRichTextUtility.ParseSupportedRichText("A <b>bold <i><color=#ff0000>hot</color></i></b> <mark=#FFE06680>clue</mark>");

            Assert.That(runs, Has.Count.EqualTo(5));
            Assert.That(runs[0].Text, Is.EqualTo("A "));
            Assert.That(runs[1].Text, Is.EqualTo("bold "));
            Assert.That(runs[1].Bold, Is.True);
            Assert.That(runs[2].Text, Is.EqualTo("hot"));
            Assert.That(runs[2].Bold, Is.True);
            Assert.That(runs[2].Italic, Is.True);
            Assert.That(runs[2].TextColor, Is.EqualTo("#FF0000"));
            Assert.That(runs[4].Text, Is.EqualTo("clue"));
            Assert.That(runs[4].HighlightColor, Is.EqualTo("#FFE06680"));
        }

        [Test]
        public void ParseSupportedRichText_ShowsUnknownTagsAsPlainText()
        {
            var runs = DialogueRichTextUtility.ParseSupportedRichText("Hi <unknown>tag</unknown>");

            Assert.That(runs, Has.Count.EqualTo(1));
            Assert.That(runs[0].Text, Is.EqualTo("Hi <unknown>tag</unknown>"));
        }

        [Test]
        public void ParseSupportedRichText_TruncatesAndKeepsStyleState()
        {
            var runs = DialogueRichTextUtility.ParseSupportedRichText("<b>Hello world</b>", 5);

            Assert.That(runs, Has.Count.EqualTo(1));
            Assert.That(runs[0].Text, Is.EqualTo("Hello..."));
            Assert.That(runs[0].Bold, Is.True);
        }

        [Test]
        public void StrictColorValidators_RequireHashAndExpectedLength()
        {
            Assert.That(DialogueRichTextUtility.TryNormalizeTextColorCode("#ff00aa", out var textColor), Is.True);
            Assert.That(textColor, Is.EqualTo("#FF00AA"));
            Assert.That(DialogueRichTextUtility.TryNormalizeTextColorCode("ff00aa", out _), Is.False);
            Assert.That(DialogueRichTextUtility.TryNormalizeTextColorCode("#ff00aa80", out _), Is.False);

            Assert.That(DialogueRichTextUtility.TryNormalizeHighlightColorCode("#ff00aa80", out var highlightColor), Is.True);
            Assert.That(highlightColor, Is.EqualTo("#FF00AA80"));
            Assert.That(DialogueRichTextUtility.TryNormalizeHighlightColorCode("#ff00aa", out _), Is.False);
        }
    }
}
