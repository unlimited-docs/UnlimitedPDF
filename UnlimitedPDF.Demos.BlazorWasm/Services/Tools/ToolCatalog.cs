using UnlimitedPDF.Demos.BlazorWasm.Samples;

namespace UnlimitedPDF.Demos.BlazorWasm.Services.Tools;

/// <summary>
/// The single list of free tools. The hub, the home strip, the search, the scenario links, and the tests all read it.
/// Only features the library ships are listed (PR-012).
/// </summary>
public static class ToolCatalog
{
    #region Fields and Constants

    /// <summary>
    /// The hub's address without a leading slash.
    /// </summary>
    public const string HubRoute = "tools";

    /// <summary>
    /// The hub's heading.
    /// </summary>
    public const string HubHeading = "Free PDF tools";

    /// <summary>
    /// The hub's browser tab and search result title.
    /// </summary>
    public const string HubDocumentTitle = "Free PDF tools: merge, split, convert · " + SampleDemo.SiteName;

    /// <summary>
    /// The hub's line under the heading. It is also the hub's meta description.
    /// </summary>
    public const string HubSubtitle =
        "Merge, split, and convert your own files for free. Built with UnlimitedPDF, running in your browser: nothing is uploaded.";

    // Built on first use, after the tool properties below are initialized.
    private static IReadOnlyList<PdfTool>? _all;

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the tool a scenario links to, or null when no tool matches it.
    /// </summary>
    /// <param name="scenario">The scenario.</param>
    /// <returns>The matching tool, or null.</returns>
    public static PdfTool? ForScenario(SampleDemo scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return All.FirstOrDefault(tool => tool.Scenarios.Contains(scenario));
    }

    /// <summary>
    /// Returns the name a group is shown under.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns>The group's name, for example "Split and extract".</returns>
    public static string GroupName(ToolGroup group) => group switch
    {
        ToolGroup.Combine => "Combine",
        ToolGroup.SplitAndExtract => "Split and extract",
        _ => "Convert",
    };

    /// <summary>
    /// Returns the tools of a group, in catalog order.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns>The group's tools.</returns>
    public static IReadOnlyList<PdfTool> InGroup(ToolGroup group) => All.Where(tool => tool.Group == group).ToList();

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets every tool, in hub order.
    /// </summary>
    public static IReadOnlyList<PdfTool> All => _all ??= [Merge, ImagesToPdf, Split, ExtractPages, RemovePages, HtmlToPdf];

    /// <summary>
    /// Gets the groups in hub order.
    /// </summary>
    public static IReadOnlyList<ToolGroup> Groups { get; } = [ToolGroup.Combine, ToolGroup.SplitAndExtract, ToolGroup.Convert];

    /// <summary>
    /// Gets the Extract pages tool.
    /// </summary>
    public static PdfTool ExtractPages { get; } = new(
        "extract-pages",
        "Extract pages",
        "Extract PDF pages",
        "Get a new PDF with only the pages you pick, right in your browser.",
        ToolGroup.SplitAndExtract,
        "Make a new PDF with only the pages you pick.",
        "1 PDF · result: 1 PDF",
        "extract",
        "Extract pages from a PDF free, in your browser",
        [SampleCatalog.ExtractPages],
        [
            new ToolStep("Add your PDF", "Choose a file or drop it on the page. It's read in your browser, never uploaded."),
            new ToolStep("Pick the pages", "Click the pages you want, or type them, for example 1, 3-5."),
            new ToolStep("Extract and download", "Press Extract and download a PDF with just those pages."),
        ],
        ["extract", "pick pages", "select pages", "copy pages", "keep pages"]);

    /// <summary>
    /// Gets the HTML to PDF tool.
    /// </summary>
    public static PdfTool HtmlToPdf { get; } = new(
        "html-to-pdf",
        "HTML to PDF",
        "Convert HTML to PDF",
        "Paste HTML or choose an .html file, add the pictures it uses, and get a PDF.",
        ToolGroup.Convert,
        "Paste HTML or choose an .html file and get a PDF.",
        "HTML · result: 1 PDF",
        "html",
        "Convert HTML to PDF free, in your browser",
        [SampleCatalog.HtmlToPdf],
        [
            new ToolStep("Add your HTML", "Paste it, or choose an .html file. It's read in your browser, never uploaded."),
            new ToolStep("Add its pictures", "Add the picture files your HTML names, such as logo.png. Web addresses aren't fetched."),
            new ToolStep("Convert and download", "Press Convert, check the preview, and download the PDF."),
        ],
        ["html", "convert", "web page", "webpage"]);

    /// <summary>
    /// Gets the Images to PDF tool.
    /// </summary>
    public static PdfTool ImagesToPdf { get; } = new(
        "images-to-pdf",
        "Images to PDF",
        "Images to PDF",
        "Turn JPG and PNG pictures into one PDF, one picture per page.",
        ToolGroup.Combine,
        "Turn JPG and PNG pictures into one PDF, one per page.",
        "Images · result: 1 PDF",
        "image",
        "Convert JPG and PNG images to PDF free, in your browser",
        [SampleCatalog.Images],
        [
            new ToolStep("Add your pictures", "Choose JPG or PNG files or drop them on the page. They're read in your browser, never uploaded."),
            new ToolStep("Order and size", "Put the pictures in order, then pick the page size and how each picture fills its page."),
            new ToolStep("Make the PDF", "Press the button and download one PDF with a picture on each page."),
        ],
        ["image", "images", "jpg", "jpeg", "png", "photo", "picture", "convert"]);

    /// <summary>
    /// Gets the Merge PDFs tool.
    /// </summary>
    public static PdfTool Merge { get; } = new(
        "merge",
        "Merge PDFs",
        "Merge PDF files",
        "Join several PDFs into one, in the order you choose, right in your browser.",
        ToolGroup.Combine,
        "Join several PDFs into one, in the order you choose.",
        "PDF files · result: 1 PDF",
        "merge",
        "Merge PDF files free, in your browser",
        [SampleCatalog.JoinFiles],
        [
            new ToolStep("Add your PDFs", "Choose files or drop them on the page. They're read in your browser, never uploaded."),
            new ToolStep("Put them in order", "Drag the files, or use the arrow buttons, until they're in the order you want."),
            new ToolStep("Merge and download", "Press Merge, check the preview, and download one PDF."),
        ],
        ["merge", "combine", "join", "append"]);

    /// <summary>
    /// Gets the Remove pages tool.
    /// </summary>
    public static PdfTool RemovePages { get; } = new(
        "remove-pages",
        "Remove pages",
        "Remove PDF pages",
        "Delete the pages you don't need and keep the rest, in order, right in your browser.",
        ToolGroup.SplitAndExtract,
        "Delete the pages you don't need and keep the rest.",
        "1 PDF · result: 1 PDF",
        "remove",
        "Remove pages from a PDF free, in your browser",
        [SampleCatalog.RemovePages],
        [
            new ToolStep("Add your PDF", "Choose a file or drop it on the page. It's read in your browser, never uploaded."),
            new ToolStep("Mark the pages", "Click the pages to remove, or type them, for example 2, 6."),
            new ToolStep("Remove and download", "Press Remove and download the PDF without those pages."),
        ],
        ["remove", "delete pages", "delete", "drop pages"]);

    /// <summary>
    /// Gets the Split PDF tool.
    /// </summary>
    public static PdfTool Split { get; } = new(
        "split",
        "Split PDF",
        "Split a PDF",
        "Cut one PDF into several files, every few pages or at the page ranges you type.",
        ToolGroup.SplitAndExtract,
        "Cut a PDF every N pages or at the page ranges you type.",
        "1 PDF · result: several PDFs",
        "split",
        "Split a PDF into several files free, in your browser",
        [SampleCatalog.SplitEveryN, SampleCatalog.SplitRanges],
        [
            new ToolStep("Add your PDF", "Choose a file or drop it on the page. It's read in your browser, never uploaded."),
            new ToolStep("Choose where to cut", "Cut every N pages, or type page ranges such as 1-2, 3-7."),
            new ToolStep("Split and download", "Press Split and download each part, or all of them as one zip."),
        ],
        ["split", "cut", "separate", "divide", "burst"]);

    #endregion
}
