using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnlimitedPDF.Models.Internal.Rendering;

namespace UnlimitedPDF.Tests.Rendering;

/// <summary>
/// The page size the preview reserves before drawing a page, the ids each drawn page keeps to its own prefix, and the
/// browser's allowlist that every drawing passes (26111701 T-12 review: H1, M2, and the size and allowlist tests).
/// </summary>
public class PageRendererSizeTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [InlineData("", 595, 842)]
    [InlineData("/CropBox [0 0 200 100]", 200, 100)]
    [InlineData("/CropBox [0 0 200 100] /Rotate 90", 100, 200)]
    [InlineData("/CropBox [0 0 200 100] /Rotate 270", 100, 200)]
    [InlineData("/CropBox [0 0 200 100] /Rotate -180", 200, 100)]
    [InlineData("/CropBox [-50 -50 900 900]", 595, 842)]
    public void The_size_is_the_visible_box_turned_by_the_rotation_and_matches_the_drawing(string entries, double width, double height)
    {
        var file = PdfFile.FromBytes(RenderTestFiles.OnePage("0 0 10 10 re f", pageEntries: entries));

        var size = PdfPageRenderer.Size(file, 0);
        var page = PdfPageRenderer.Render(file, 0, "s-");

        Assert.Equal((width, height), size);
        Assert.Equal((page.Width, page.Height), size);
    }

    [Fact]
    public void A_page_whose_size_cant_be_read_has_none_and_an_index_out_of_range_throws()
    {
        var bad = PdfFile.FromBytes(RenderTestFiles.OnePage("0 0 10 10 re f", pageEntries: "/Rotate 45"));

        Assert.Null(PdfPageRenderer.Size(bad, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfPageRenderer.Size(bad, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfPageRenderer.Size(bad, -1));
        Assert.Throws<ArgumentNullException>(() => PdfPageRenderer.Size(null!, 0));
    }

    [Fact]
    public void Each_page_of_the_preview_has_ids_of_its_own()
    {
        // The preview places every page in one HTML document, where a reference finds the first element with an id, so
        // a page must never use another page's id. Every id a page writes starts with that page's prefix.
        var pages = RenderTestFiles.RenderAll(Merged());
        Assert.True(pages.Count >= 3);
        for (int i = 0; i < pages.Count; i++)
        {
            var ids = Regex.Matches(pages[i].Svg!, " id=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
            Assert.NotEmpty(ids);
            Assert.All(ids, id => Assert.StartsWith($"t{i}-", id, StringComparison.Ordinal));
        }

        Assert.Contains("PagePrefix(version, number)", DemosFile("Components/PdfPreview.razor.cs"), StringComparison.Ordinal);
        Assert.Contains("=> $\"{_prefix}v{version}p{number}-\";", DemosFile("Components/PdfPreview.razor.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void The_browser_allowlist_keeps_references_inside_the_drawing()
    {
        // A paint or clip may name only an element of the drawing; any other url(...) is dropped (M2). Ids keep to
        // the renderer's characters.
        var script = DemosFile("wwwroot/js/app.js");

        Assert.Contains("(!/url\\s*\\(/i.test(value) || /^url\\(#[A-Za-z0-9_-]+\\)$/.test(value))", script, StringComparison.Ordinal);
        Assert.Contains("(attribute.name !== \"id\" || /^[A-Za-z0-9_-]+$/.test(value))", script, StringComparison.Ordinal);
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Returns the element and attribute names the browser lets through, read from <c>js/app.js</c>.
    /// </summary>
    internal static (HashSet<string> Elements, HashSet<string> Attributes) Allowlist()
    {
        var script = DemosFile("wwwroot/js/app.js");
        static HashSet<string> Set(string script, string name) =>
            Regex.Matches(Regex.Match(script, $@"const {name} = new Set\(\[(.*?)\]\);", RegexOptions.Singleline).Groups[1].Value, "\"([^\"]+)\"")
                .Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        return (Set(script, "svgElements"), Set(script, "svgAttributes"));
    }

    #endregion

    #region Private Methods

    private static string DemosFile(string relative, [CallerFilePath] string source = "") =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", "UnlimitedPDF.Demos.BlazorWasm", relative));

    // Three invoices, each with its own logo image and clipped table cells, merged into one file: every page defines
    // images and clips of its own.
    private static byte[] Merged()
    {
        var image = PdfImage.FromBytes(new JpegBuilder { Width = 120, Height = 60 }.Build());
        var images = new UnlimitedPDF.Demos.BlazorWasm.Samples.SampleImages(new[] { "invoice-logo.png", "keyboard.jpg", "dock.jpg" }.ToDictionary(n => n, _ => image));
        var document = new PdfDocument();
        for (int i = 0; i < 3; i++)
        {
            var invoice = UnlimitedPDF.Demos.BlazorWasm.Samples.BulkInvoicesSample.Invoice(new UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs.BulkInvoicesInput(), i);
            document.AddPages(PdfFile.FromBytes(PdfPageReader.SaveBytes(UnlimitedPDF.Demos.BlazorWasm.Samples.InvoiceSample.Build(invoice, images))));
        }

        // Each imported file keeps its own copy of the shared logo, so the merge is saved without the check that a
        // built document stores each image once.
        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    #endregion

    #endregion
}
