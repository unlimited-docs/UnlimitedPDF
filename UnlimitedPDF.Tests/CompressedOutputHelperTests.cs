using System.Text.RegularExpressions;
using UnlimitedPDF;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The test helpers that read saved PDFs (<see cref="PdfPageReader"/>, <see cref="PdfInspector"/>, and
/// <see cref="PdfStructureChecker"/>) give the same answer for a file whose streams are compressed with
/// <c>/Filter /FlateDecode</c> as for the same file written plain (WR-042, PR-029). The compressed copy is made here by
/// rewriting every stream of a library-written file, so the helpers are proven before the library compresses anything.
/// </summary>
public class CompressedOutputHelperTests
{
    #region Fields and Constants

    private static readonly Regex LengthEntry = new(@"/Length \d+", RegexOptions.CultureInvariant);
    private static readonly Regex TrailerReference = new(@"/(Root|Info) \d+ 0 R", RegexOptions.CultureInvariant);

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Page_reader_reads_compressed_pages_as_their_plain_form()
    {
        var plain = SaveSample();
        var compressed = CompressEveryStream(plain);

        var expected = PdfPageReader.Read(plain);
        var actual = PdfPageReader.Read(compressed);

        // The copy compresses every page content stream and the ToUnicode map; the font program is compressed in both.
        Assert.Equal(FlateStreams(plain) + expected.Count + 1, FlateStreams(compressed));

        Assert.Equal(expected, actual);
        Assert.Contains(actual, p => p.Shows("Plain page one"));
        Assert.Contains(actual, p => p.LayoutTexts().Any(t => t.Text == "Flow line"));
        Assert.Equal(expected.Select(p => p.LayoutRectangles()), actual.Select(p => p.LayoutRectangles()));
        Assert.Equal(expected.Select(p => p.FilledRectangles()), actual.Select(p => p.FilledRectangles()));
        Assert.Equal(2, actual.Sum(p => p.LayoutRectangles().Count + p.FilledRectangles().Count));
    }

    [Fact]
    public void Inspector_reads_compressed_text_and_fonts_as_their_plain_form()
    {
        var plain = SaveSample();
        var compressed = CompressEveryStream(plain);

        var expected = PdfInspector.Read(plain);
        var actual = PdfInspector.Read(compressed);

        Assert.Equal(expected.PageText(), actual.PageText());
        Assert.Contains(actual.PageText(), t => t.Contains("ABBA", StringComparison.Ordinal));
        var expectedFont = expected.Fonts().Single();
        var actualFont = actual.Fonts().Single();
        Assert.Equal(expectedFont.Program, actualFont.Program);
        Assert.Equal(expectedFont.GlyphCount, actualFont.GlyphCount);
    }

    [Fact]
    public void Structure_checker_accepts_a_compressed_copy_of_a_valid_file()
    {
        var compressed = CompressEveryStream(SaveSample());

        Assert.Empty(PdfStructureChecker.Check(compressed));
    }

    #endregion

    #region Private Methods

    // Rewrites every stream that has no filter as a /FlateDecode stream with its true /Length, and keeps every other
    // object and every already-filtered stream as written, so only the encoding of the plain streams differs.
    private static byte[] CompressEveryStream(byte[] pdf)
    {
        var (objects, _) = PdfStructureChecker.ReadObjects(pdf);
        var builder = new ReaderFixtureBuilder();
        foreach (var (number, (dictionary, stream)) in objects.OrderBy(o => o.Key))
        {
            if (stream is null)
            {
                builder.AddObject(number, dictionary.Trim());
                continue;
            }

            var entries = dictionary.Trim();
            Assert.StartsWith("<<", entries, StringComparison.Ordinal);
            Assert.EndsWith(">>", entries, StringComparison.Ordinal);
            entries = LengthEntry.Replace(entries[2..^2], "").Trim();
            if (entries.Contains("/Filter", StringComparison.Ordinal))
                builder.AddStream(number, entries, stream); // already encoded (the font program): kept as written
            else
                builder.AddStream(number, $"{entries} /Filter /FlateDecode", ReaderFixtureBuilder.Deflate(stream));
        }

        var text = Encoding.Latin1.GetString(pdf);
        var trailer = text[text.LastIndexOf("trailer", StringComparison.Ordinal)..];
        return builder.ToBytes(trailerEntries: string.Join(" ", TrailerReference.Matches(trailer).Select(m => m.Value)));
    }

    private static int FlateStreams(byte[] pdf) =>
        Regex.Matches(Encoding.Latin1.GetString(pdf), "/Filter /FlateDecode", RegexOptions.CultureInvariant).Count;

    // Standard-font text, layout text and a rectangle from the body flow, a page rectangle, and embedded-font text with
    // a ToUnicode map and a font program. Saved uncompressed, so the plain form is the reference.
    private static byte[] SaveSample()
    {
        var builder = new TestFontBuilder();
        int a = builder.AddGlyph(600, TestFontBuilder.Box(0, 0, 100, 100));
        int b = builder.AddGlyph(500, TestFontBuilder.Box(0, 0, 80, 120));
        builder.Map('A', a);
        builder.Map('B', b);
        var font = PdfFont.FromBytes(builder.Build());

        var document = new PdfDocument { CompressStreams = false };
        document.AddPage().AddText("Plain page one", 50, 700, 12);
        document.Body.AddText("Flow line");
        document.Body.AddRectangle(100, 10, PdfColor.Black, spacing: 4, x: 20);
        var page = document.AddPage();
        page.AddRectangle(40, 40, 200, 30, PdfColor.LightGray);
        page.AddText("ABBA", font, 50, 600, 14);
        return PdfPageReader.SaveBytes(document);
    }

    #endregion

    #endregion
}
