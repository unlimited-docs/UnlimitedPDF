namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// The FAQ page's topics and questions. Every answer is true of the library as shipped (PR-012), and a question about
/// something it does not do yet says so.
/// </summary>
public static class FaqQuestions
{
    #region Fields and Constants

    // Built on first use, after the catalogue's demo properties exist.
    private static IReadOnlyList<FaqGroup>? _groups;

    #endregion

    #region Methods

    #region Private Methods

    private static IReadOnlyList<FaqGroup> Build() =>
    [
        new("getting-started", "Getting started and platforms",
        [
            new("install", "How do I install it?",
            [
                "Add the UnlimitedPDF package from NuGet: run \"dotnet add package UnlimitedPDF\" in your project's folder, or add a PackageReference to UnlimitedPDF in the project file."
            ]),
            new("native-libraries", "Does it need native libraries or a server?",
            [
                "No. UnlimitedPDF is managed C# with no native code, no PDF engine to install, and no third-party package dependencies.",
                "Every demo on this site builds its PDF inside your browser, with nothing sent to a server."
            ], SampleCatalog.TextAndPages),
            new("dotnet-versions", "Which .NET versions does it support?",
            [
                "One package targets .NET 6, .NET 8, .NET 9, and .NET 10."
            ]),
            new("blazor-webassembly", "Does it run in Blazor WebAssembly?",
            [
                "Yes. This site is a Blazor WebAssembly app: each demo builds its PDF in the browser with the same UnlimitedPDF library, then shows it or saves it."
            ], SampleCatalog.TextAndPages),
            new("linux-and-containers", "Does it run on Linux, macOS, and in containers?",
            [
                "It has no native code, so it runs wherever your .NET app runs, including Linux and macOS servers and containers.",
                "Text uses the PDF standard fonts, which PDF viewers provide, so no fonts have to be installed on the machine."
            ])
        ]),
        new("text-and-fonts", "Text and fonts",
        [
            new("question-marks", "Why do some characters show as \"?\"",
            [
                "Text added with AddText, in tables, and in headers and footers is written in ASCII, so any other character, even an accented letter, becomes a question mark.",
                "HTML content supports the wider WinAnsi set: ASCII plus accented Latin letters, the euro sign, and typographic quotes and dashes. Characters outside it, such as Greek, Cyrillic, Chinese, or emoji, still become a question mark."
            ], SampleCatalog.CommonProblems),
            new("own-fonts", "Can I use my own fonts?",
            [
                "Not yet. Text uses the PDF standard fonts: AddText, tables, headers, and footers use Helvetica, and HTML can choose Helvetica, Times, or Courier, each in regular, bold, italic, and bold italic."
            ], NotYet: true),
            new("wrap-text", "Does text wrap onto the next line?",
            [
                "Text added with AddText stays on one line. For paragraphs that wrap, add HTML to the body flow: its lines wrap at the body's width and continue onto new pages."
            ], SampleCatalog.HtmlToPdf, "wrap-paragraph"),
            new("align-text", "Can I center or right-align text?",
            [
                "Yes, in the body flow and in headers and footers: pass PdfTextAlignment.Center or PdfTextAlignment.Right. Table cells have their own horizontal alignment setting."
            ], SampleCatalog.HeadersFooters, "center-text")
        ]),
        new("tables-and-layout", "Tables and layout",
        [
            new("tables-across-pages", "Do tables continue onto the next page?",
            [
                "Yes. A table made with AddTable continues row by row onto new pages when it reaches the bottom of the page's body. Its rows are never split, and header rows can repeat at the top of each page."
            ], SampleCatalog.EmployeeDirectory, "repeat-header"),
            new("page-numbers", "How do I add page numbers?",
            [
                "Give the document a footer or a header and put {page} and {pages} in its text. Each page's number and the page count are filled in when the document is saved."
            ], SampleCatalog.HeadersFooters, "page-numbers"),
            new("units", "What unit are positions and sizes in?",
            [
                "Points: 72 points make an inch. Positions on a page are measured from its bottom-left corner.",
                "PdfUnits converts millimeters, centimeters, inches, and pixels (96 to the inch) to points."
            ], SampleCatalog.TextAndPages, "units"),
            new("page-sizes", "Which page sizes can I use?",
            [
                "A0 to A6, B4, B5, Letter, Legal, Tabloid, Ledger, Executive, Statement, and Half Letter, chosen with PdfPageSize. A4 is the default.",
                "Changing a document's page size affects the pages added after the change."
            ])
        ]),
        new("images", "Images",
        [
            new("image-formats", "Which image formats are supported?",
            [
                "JPEG and PNG, including PNG transparency. Other formats, such as WebP, GIF, or SVG, are rejected when the image is loaded, with a PdfImageException."
            ], SampleCatalog.Images),
            new("add-image", "How do I add an image?",
            [
                "Load it once with PdfImage, from a file, a stream, or bytes, then place it on a page, in the body flow, in a header or footer, or in a table cell. Give a width, a height, or both; with one, the other keeps the image's proportions."
            ], SampleCatalog.Images, "add-image"),
            new("image-size", "How big is an image placed without a size?",
            [
                "It is drawn at 96 pixels to the inch, so an image 300 pixels wide is 225 points (about 79 mm) wide. Resolution settings stored in the file are not used."
            ], SampleCatalog.Images)
        ]),
        new("merge-and-split", "Merge and split",
        [
            new("merge-pdfs", "How do I merge PDFs?",
            [
                "Load each file as a PdfFile and pass them to PdfOperations.Merge. Pages keep their look and links, and each file gets a bookmark that holds its own bookmarks."
            ], SampleCatalog.JoinFiles, "merge"),
            new("pick-pages", "Can I take only some pages from a PDF?",
            [
                "Yes. PdfDocument.AddPages adds a whole file or a range of its pages, and imported pages can be mixed with pages you build."
            ], SampleCatalog.PickPages, "pick-pages"),
            new("split-pdf", "How do I split a PDF?",
            [
                "PdfOperations.Split writes one file every N pages, or one file per page range, to a folder or to streams you provide."
            ], SampleCatalog.SplitEveryN, "split"),
            new("password-protected", "Can it open password-protected PDFs?",
            [
                "Not yet. Encrypted PDFs, including ones that open without a password but restrict printing or copying, are rejected when they are loaded, with a PdfFileException."
            ], NotYet: true)
        ]),
        new("html-to-pdf", "HTML to PDF",
        [
            new("convert-html", "How do I convert HTML to PDF?",
            [
                "Load the HTML with PdfHtml and call PdfOperations.ConvertHtml. To mix it with other content, add it to a document's body flow with AddHtml instead."
            ], SampleCatalog.HtmlToPdf, "convert-html"),
            new("css-support", "Which HTML and CSS are supported?",
            [
                "A built-in renderer lays out common HTML, such as headings, paragraphs, lists, tables, images, and links, styled by style sheets and style attributes.",
                "It runs no scripts and has no flexbox, grid, or floats. Content that uses them falls back to a simpler layout, and the loaded HTML lists a warning for it."
            ], SampleCatalog.CommonProblems),
            new("html-images", "Can HTML load images from the web?",
            [
                "No. The library never makes a network request. Images and style sheets load from data: URIs, from files in the HTML file's own folder or a base folder you set, or through a callback you supply."
            ], SampleCatalog.CommonProblems, "html-from-file")
        ]),
        new("license-and-support", "License and support",
        [
            new("commercial-use", "Can I use it in commercial software?",
            [
                "Yes. UnlimitedPDF is released under the Apache-2.0 license, which allows commercial use."
            ]),
            new("sign-or-encrypt", "Can it sign or password-protect the PDFs it creates?",
            [
                "Not yet. The PDFs it writes have no password, encryption, or digital signature."
            ], NotYet: true),
            new("pdf-a", "Can it create PDF/A files?",
            [
                "Not yet. It writes standard PDF, version 1.7 or the higher version of a PDF whose pages it imports, not PDF/A."
            ], NotYet: true),
            new("source-code", "Where is the source code?",
            [
                "On GitHub, at github.com/unlimited-docs/UnlimitedPDF. The library, this demo site, and their tests are all in that repository."
            ])
        ])
    ];

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the topics in the order the page shows them.
    /// </summary>
    public static IReadOnlyList<FaqGroup> Groups => _groups ??= Build();

    /// <summary>
    /// Gets every question, in page order.
    /// </summary>
    public static IEnumerable<FaqQuestion> Questions => Groups.SelectMany(group => group.Questions);

    #endregion
}
