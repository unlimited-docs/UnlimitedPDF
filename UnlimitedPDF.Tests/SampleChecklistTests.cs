using System.Text.RegularExpressions;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The code a developer copies from a demo uses the current API without workarounds (26110501 AC-25 (c)): no hand
/// escaping of text, no centering estimated from character counts, and no tables standing in for bands, boxes, or
/// outlines. Checks the source of every playground sample and the console demo, and proves each check catches the
/// workaround forms it is meant to catch.
/// </summary>
public class SampleChecklistTests
{
    #region Fields and Constants

    // A cell's background and a border set to the same color, in either order: a table drawn only to fill a rectangle.
    private static readonly Regex TableBand = new(
        @"\.BackgroundColor = ([^;\r\n]+);[\s\S]{0,400}?\.Color = \1;|\.Color = ([^;\r\n]+);[\s\S]{0,400}?\.BackgroundColor = \2;",
        RegexOptions.CultureInvariant);

    // A row added and thrown away, or a new row's cell given borders but no text or content: a table drawn only for
    // its outline.
    private static readonly Regex TableOutline = new(
        @"(?m)^\s*\w+\.AddRow\(\);|var (\w+) = \w+\.AddRow\(\)\.GetCell\(\d+\);(?![\s\S]{0,400}?\b\1\.(?:Text|SetContent|AddContent)\b)[\s\S]{0,400}?\b\1\.Border\w*\b",
        RegexOptions.CultureInvariant);

    // A width worked out from the number of characters, the way text was centered before alignment existed.
    private static readonly Regex EstimatedWidth = new(@"\.Length\s*\*|\*\s*\w+(?:\.\w+)*\.Length\b", RegexOptions.CultureInvariant);

    // Text escaped by hand: the library escapes all text (WR-005).
    private static readonly Regex HandEscaping = new(@"PdfText\.Escape|\.Replace\(\s*@?[""'][\\()]", RegexOptions.CultureInvariant);

    #endregion

    #region Methods

    #region Public Methods

    [Theory]
    [MemberData(nameof(Sources))]
    public void Sample_code_uses_the_current_api_without_workarounds(string file)
    {
        var source = File.ReadAllText(SourcePath(file));

        Assert.Empty(Workarounds(source).Select(problem => $"{file} {problem}"));
    }

    [Theory]
    // Bands: the background and border color as a field, a static color, a new color, and the border set first.
    [InlineData("cell.BackgroundColor = color;\nforeach (var border in borders) border.Color = color;", "band")]
    [InlineData("cell.BackgroundColor = PdfColor.Black;\ncell.BorderLeft.Color = PdfColor.Black;", "band")]
    [InlineData("cell.BackgroundColor = new PdfColor(0.1, 0.2, 0.3);\ncell.BorderTop.Color = new PdfColor(0.1, 0.2, 0.3);", "band")]
    [InlineData("cell.BorderBottom.Color = _gold;\ncell.BackgroundColor = _gold;", "band")]
    // Outlines: a thrown-away row, and a cell given borders and nothing else.
    [InlineData("var frame = page.AddTable(50, 700, 100);\nframe.RowHeight = 80;\nframe.AddRow();", "outline")]
    [InlineData("var cell = frame.AddRow().GetCell(0);\nforeach (var border in new[] { cell.BorderLeft, cell.BorderTop })\n    border.Width = 1;", "outline")]
    // Widths from character counts.
    [InlineData("int x = (int)((pageWidth - text.Length * fontSize * 0.5) / 2);", "width")]
    [InlineData("double width = title.Length * size;", "width")]
    [InlineData("double width = size * title.Length;", "width")]
    // Hand escaping.
    [InlineData("page.AddText(PdfText.Escape(name), 50, 700, 12);", "escaping")]
    [InlineData("var safe = name.Replace(\"\\\\\", \"\\\\\\\\\").Replace(\"(\", \"\\\\(\");", "escaping")]
    [InlineData("var safe = name.Replace(\")\", \"\\\\)\");", "escaping")]
    [InlineData("var safe = name.Replace(@\"\\\", @\"\\\\\");", "escaping")]
    // Current API use that must pass.
    [InlineData("cell.BackgroundColor = _tint;\ncell.Text = \"Total\";", null)]
    [InlineData("var cell = table.AddRow().GetCell(0);\ncell.Text = \"Name\";\ncell.BorderBottom.Color = _gray;", null)]
    [InlineData("var row = table.AddRow();\nrow.GetCell(0).Text = \"a\";", null)]
    [InlineData("for (int column = 0; column < names.Length; column++) labels.GetCell(column).Text = names[column];", null)]
    [InlineData("page.AddText(name, 50, 700, 12, PdfTextAlignment.Center);", null)]
    [InlineData("var label = code.Replace(\"-\", \" \");", null)]
    public void Each_check_catches_its_workaround_and_passes_current_api_use(string source, string? expected)
    {
        var problems = Workarounds(source);

        if (expected is null) Assert.Empty(problems);
        else Assert.Equal(expected, Assert.Single(problems).Split(':')[0]);
    }

    #endregion

    #region Private Methods

    private static string SamplesDirectory() => Path.Combine(RepositoryRoot(), "UnlimitedPDF.Demos.BlazorWasm", "Samples");

    private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Fixtures.DemoAssets(), "..", ".."));

    private static string SourcePath(string file) => file == "DemoDocument.cs"
        ? Path.Combine(RepositoryRoot(), "UnlimitedPDF.Demo.ConsoleApp", file)
        : Path.Combine(SamplesDirectory(), file);

    private static List<string> Workarounds(string source)
    {
        var problems = new List<string>();
        if (TableBand.IsMatch(source)) problems.Add("band: draws a band as a one-cell table; use AddRectangle.");
        if (TableOutline.IsMatch(source)) problems.Add("outline: draws a box outline as a table; use four AddRectangle lines.");
        if (EstimatedWidth.IsMatch(source)) problems.Add("width: estimates text width from its character count; use PdfTextAlignment.");
        if (HandEscaping.IsMatch(source)) problems.Add("escaping: escapes text by hand; the library escapes all text.");
        return problems;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    public static TheoryData<string> Sources
    {
        get
        {
            var data = new TheoryData<string> { "DemoDocument.cs" };
            foreach (var path in Directory.GetFiles(SamplesDirectory(), "*Sample.cs").OrderBy(p => p, StringComparer.Ordinal))
                data.Add(Path.GetFileName(path));
            return data;
        }
    }

    #endregion
}
