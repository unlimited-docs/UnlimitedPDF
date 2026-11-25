using System.Diagnostics;
using UnlimitedPDF.Models.Internal.Text;
using UnlimitedPDF.Models.Internal.Text.Bidi;

namespace UnlimitedPDF.Tests.Text;

/// <summary>
/// The bidirectional algorithm on the story's examples (WR-041, V-03, V-13), run in every build: paragraph direction,
/// numbers and brackets in right-to-left text, isolates, overrides, marks, trailing whitespace, mirroring, and the
/// limits that keep hostile input linear and free of exceptions. Hebrew letters stand for right-to-left text.
/// </summary>
public class BidiParagraphTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [InlineData("Hello", 0)]
    [InlineData("שלום", 1)]
    [InlineData("سلام", 1)]
    [InlineData("ހެލޯ", 1)]
    [InlineData("123 שקל", 1)] // numbers are weak; the first strong letter decides
    [InlineData("iPhone 15 מחיר", 0)] // the first strong letter is Latin
    [InlineData("⁧שלום⁩ hello", 0)] // an isolate is skipped
    [InlineData("123", null)]
    [InlineData("", null)]
    public void The_first_strong_character_sets_the_paragraph_direction(string text, int? expected) =>
        Assert.Equal(expected, BidiParagraph.FirstStrongLevel(text));

    [Fact]
    public void Right_to_left_words_read_from_the_right_and_left_to_right_words_keep_their_order()
    {
        // ORDER (abc 12) DONE, with Hebrew for the uppercase words, in a right-to-left paragraph.
        string text = "אבג (abc 12) דה";
        Assert.Equal("הד (abc 12) גבא", Visual(text, 1));
    }

    [Fact]
    public void Numbers_keep_their_digit_order_inside_right_to_left_text()
    {
        Assert.Equal("1,234.50 ₪ :םוכס", Visual("סכום: ₪ 1,234.50", 1));
        Assert.Equal("١٢٣ ددع", Visual("عدد ١٢٣", 1));
        Assert.Equal("۱۴۰۳ لاس", Visual("سال ۱۴۰۳", 1));
    }

    [Fact]
    public void An_isolate_keeps_its_punctuation_with_it()
    {
        // HE SAID ⁧hello!⁩ TODAY, as a left-to-right isolate inside right-to-left text.
        string text = "אמר ⁦hello!⁩ היום";
        Assert.Equal("םויה hello! רמא", Visual(text, 1));
    }

    [Fact]
    public void An_override_draws_its_text_in_the_forced_direction()
    {
        Assert.Equal("cba", Visual("‮abc‬", 0));
        Assert.Equal("אבג", Visual("‭אבג‬", 1));
    }

    [Fact]
    public void A_right_to_left_mark_makes_a_line_that_starts_with_latin_read_right_to_left()
    {
        var paragraph = BidiParagraph.Create("‏iPhone 15 מחיר", null);
        Assert.Equal(1, paragraph.BaseLevel);
        Assert.Equal("ריחמ iPhone 15", Visual("‏iPhone 15 מחיר", null));
    }

    [Fact]
    public void Brackets_in_right_to_left_text_are_drawn_mirrored()
    {
        var paragraph = BidiParagraph.Create("אב (גד)", 1);
        int open = "אב (גד)".IndexOf('(');
        Assert.True(paragraph.IsMirrored(open));
        Assert.False(BidiParagraph.Create("ab (cd)", 0).IsMirrored(3));
    }

    [Fact]
    public void Trailing_whitespace_returns_to_the_paragraph_level()
    {
        var paragraph = BidiParagraph.Create("abc אבג   ", 0);
        var levels = paragraph.LineLevels(0, paragraph.Count);
        Assert.Equal(new byte[] { 0, 0, 0, 0, 1, 1, 1, 0, 0, 0 }, levels);
    }

    [Fact]
    public void Format_characters_are_removed_from_drawing()
    {
        var paragraph = BidiParagraph.Create("a‫b‬c‍", 0);
        Assert.True(paragraph.IsRemoved(1));
        Assert.True(paragraph.IsRemoved(3));
        Assert.True(paragraph.IsRemoved(5));
        Assert.Equal(new[] { 0, 2, 4 }, paragraph.VisualOrder(0, paragraph.Count));
    }

    [Fact]
    public void Unmatched_and_overflowing_controls_never_throw()
    {
        string text = "‬⁩a⁧b" + new string('‫', 200) + "c" + new string('⁦', 200) + "d" + new string('‬', 300) + "⁩⁩e";
        var paragraph = BidiParagraph.Create(text, null);

        Assert.All(Enumerable.Range(0, paragraph.Count), i => Assert.InRange(paragraph.LevelAt(i), 0, BidiParagraph.MaxDepth + 1));
        Assert.Equal(paragraph.Count - Enumerable.Range(0, paragraph.Count).Count(paragraph.IsRemoved), paragraph.VisualOrder(0, paragraph.Count).Length);
    }

    [Fact]
    public void A_long_mixed_line_with_deep_nesting_resolves_within_the_time_bound()
    {
        // 80,000 characters: Arabic, Hebrew, Latin, numbers, brackets, 10,000 nested embeddings and isolates, and
        // unmatched terminators (A06, V-13).
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 5_000; i++) sb.Append(i % 2 == 0 ? '‫' : '⁦');
        while (sb.Length < 70_000) sb.Append("سلام (abc 12) שלום [x] ");
        sb.Append(new string('⁩', 5_000)).Append(new string('‬', 5_000));
        string text = sb.ToString(0, 80_000);
        BidiParagraph.Create("warm up (a) ب", null);
        var clock = Stopwatch.StartNew();

        var paragraph = BidiParagraph.Create(text, null);
        var order = paragraph.VisualOrder(0, paragraph.Count);

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3), clock.Elapsed.ToString());
        Assert.Equal(order.Length, order.Distinct().Count());
    }

    #endregion

    #region Private Methods

    // The text of a single-line paragraph as drawn, left to right: visual order, format characters dropped, mirrored
    // characters swapped.
    private static string Visual(string text, int? baseLevel)
    {
        var paragraph = BidiParagraph.Create(text, baseLevel);
        var sb = new System.Text.StringBuilder();
        foreach (int index in paragraph.VisualOrder(0, paragraph.Count))
        {
            if (paragraph.IsFormat(index)) continue;
            int codePoint = CodePointAt(text, index);
            sb.Append(char.ConvertFromUtf32(paragraph.IsMirrored(index) ? BidiData.MirrorOf(codePoint) : codePoint));
        }
        return sb.ToString();
    }

    private static int CodePointAt(string text, int codePointIndex)
    {
        int i = 0;
        for (int k = 0; k < codePointIndex; k++) i += char.IsSurrogatePair(text, i) ? 2 : 1;
        return char.ConvertToUtf32(text, i);
    }

    #endregion

    #endregion
}
