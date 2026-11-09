using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Tests.Support;

/// <summary>
/// Source PDFs and read-back helpers for the split tests (PR-011: generated in test code). Page n of a
/// <see cref="ReaderFixtureBuilder.Document"/> source is object 2n + 2 and shows the text "Page n".
/// </summary>
internal static class SplitFixtures
{
    #region Methods

    #region Internal Methods

    /// <summary>
    /// Ten pages with bookmarks: Intro (page 1), Sales (page 4, open) holding Europe (page 5), and Costs (page 8).
    /// </summary>
    internal static byte[] Bookmarked() => ReaderFixtureBuilder.Document(10, catalogEntries: "/Outlines 40 0 R ")
        .AddObject(40, "<< /Type /Outlines /First 41 0 R /Last 44 0 R /Count 4 >>")
        .AddObject(41, $"<< /Title (Intro) /Parent 40 0 R /Next 42 0 R /Dest [{Page(1)} 0 R /Fit] >>")
        .AddObject(42, $"<< /Title (Sales) /Parent 40 0 R /Prev 41 0 R /Next 44 0 R /First 43 0 R /Last 43 0 R /Count 1 /Dest [{Page(4)} 0 R /Fit] >>")
        .AddObject(43, $"<< /Title (Europe) /Parent 42 0 R /Dest [{Page(5)} 0 R /Fit] >>")
        .AddObject(44, $"<< /Title (Costs) /Parent 40 0 R /Prev 42 0 R /Dest [{Page(8)} 0 R /Fit] >>")
        .ToBytes();

    /// <summary>
    /// Returns the top-level bookmarks of <paramref name="pdf"/>, each with its title, 1-based target page, and
    /// children; empty when the file has no outline.
    /// </summary>
    internal static List<Bookmark> Outline(byte[] pdf)
    {
        var output = PdfFile.FromBytes(pdf);
        var table = output.Source.Objects;
        var pages = output.Pages.Select((page, index) => (page.ObjectNumber, index + 1)).ToDictionary(p => p.ObjectNumber, p => p.Item2);

        List<Bookmark> Siblings(PdfReadValue? first, int depth)
        {
            var items = new List<Bookmark>();
            var visited = new HashSet<int>();
            for (var current = first; current is PdfReadReference reference && visited.Add(reference.ObjectNumber) && depth < 20;)
            {
                var item = Assert.IsType<PdfReadDictionary>(table.Resolve(current));
                var destination = table.Resolve(item["Dest"]) as PdfReadArray;
                int? target = destination?[0] is PdfReadReference page && pages.TryGetValue(page.ObjectNumber, out int number) ? number : null;
                string title = PdfTextString.Decode(Assert.IsType<PdfReadString>(table.Resolve(item["Title"])).Bytes);
                items.Add(new Bookmark(title, target, Siblings(item["First"], depth + 1)));
                current = item["Next"];
            }
            return items;
        }

        return table.Resolve(output.Source.Catalog["Outlines"]) is PdfReadDictionary root
            ? Siblings(root["First"], 0)
            : new List<Bookmark>();
    }

    /// <summary>
    /// Returns the "Page n" texts of the pages of <paramref name="pdf"/>, in page order.
    /// </summary>
    internal static string[] PageTexts(byte[] pdf) => ImportedPagesTests.PageTexts(PdfFile.FromBytes(pdf));

    /// <summary>
    /// Returns the object number of page <paramref name="page"/> in a <see cref="ReaderFixtureBuilder.Document"/>
    /// source.
    /// </summary>
    internal static int Page(int page) => 2 * page + 2;

    /// <summary>
    /// Returns the "Page n" texts for pages <paramref name="first"/> to <paramref name="last"/>.
    /// </summary>
    internal static string[] Texts(int first, int last) =>
        Enumerable.Range(first, last - first + 1).Select(n => "Page " + n.ToString(CultureInfo.InvariantCulture)).ToArray();

    #endregion

    #endregion

    #region Nested Types

    /// <summary>
    /// One bookmark read back from a PDF.
    /// </summary>
    internal sealed record Bookmark(string Title, int? TargetPage, List<Bookmark> Children);

    #endregion
}
