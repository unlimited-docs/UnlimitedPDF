using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// The code samples of the home page's capability rows, as methods that compile and run. The page shows the lines
/// between each <c>// snippet:&lt;name&gt;</c> marker and the <c>// end</c> after it (PR-012), so a sample that
/// stops compiling breaks the build.
/// </summary>
public static class HomeSnippets
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// The Create sample: a one-page document with a line of text.
    /// </summary>
    /// <param name="output">The stream the PDF is written to.</param>
    public static void Create(Stream output)
    {
        // snippet:create
        var document = new PdfDocument();
        document.AddPage().AddText("Hello, PDF!", 72, 720, 24);
        document.Save(output);
        // end
    }

    /// <summary>
    /// The HTML to PDF sample: HTML converted to a PDF in one call.
    /// </summary>
    /// <param name="output">The stream the PDF is written to.</param>
    public static void Html(Stream output)
    {
        // snippet:html
        var html = PdfHtml.FromString("<h1>Hello</h1><p>Tables, lists, and <b>CSS</b> too.</p>");
        PdfOperations.ConvertHtml(html, output);
        // end
    }

    /// <summary>
    /// The Merge sample: two PDFs joined in one call.
    /// </summary>
    /// <param name="invoicePdf">The first PDF's bytes.</param>
    /// <param name="receiptPdf">The second PDF's bytes.</param>
    /// <param name="output">The stream the joined PDF is written to.</param>
    public static void Merge(byte[] invoicePdf, byte[] receiptPdf, Stream output)
    {
        // snippet:merge
        var invoice = PdfFile.FromBytes(invoicePdf);
        var receipt = PdfFile.FromBytes(receiptPdf);
        PdfOperations.Merge(output, invoice, receipt);
        // end
    }

    /// <summary>
    /// The Split sample: a PDF split into files of two pages each.
    /// </summary>
    /// <param name="reportPdf">The PDF's bytes.</param>
    /// <param name="outputFolder">The folder the parts are written to.</param>
    public static void Split(byte[] reportPdf, string outputFolder)
    {
        // snippet:split
        var report = PdfFile.FromBytes(reportPdf);
        PdfOperations.Split(report, pagesPerPart: 2, outputFolder);
        // end
    }

    #endregion

    #endregion
}
