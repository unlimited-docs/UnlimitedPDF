using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// The FAQ page's code samples, as methods that compile and run. The page shows the lines between each
/// <c>// snippet:&lt;name&gt;</c> marker and the <c>// end</c> after it (PR-012), so a sample that stops compiling
/// breaks the build.
/// </summary>
public static class FaqSnippets
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// A line of text centered across the page.
    /// </summary>
    /// <param name="output">The stream the PDF is written to.</param>
    public static void CenterText(Stream output)
    {
        // snippet:center-text
        var document = new PdfDocument();
        document.Body.AddText("Quarterly report", fontSize: 20, alignment: PdfTextAlignment.Center);
        document.Save(output);
        // end
    }

    /// <summary>
    /// HTML converted to a PDF in one call.
    /// </summary>
    /// <param name="output">The stream the PDF is written to.</param>
    public static void ConvertHtml(Stream output)
    {
        // snippet:convert-html
        var html = PdfHtml.FromString("<h1>Invoice</h1><p>Thank you for your order.</p>");
        PdfOperations.ConvertHtml(html, output);
        // end
    }

    /// <summary>
    /// An HTML file converted with its folder as the base folder, so images and stylesheets beside it load.
    /// </summary>
    /// <param name="htmlPath">The HTML file's path.</param>
    /// <param name="output">The stream the PDF is written to.</param>
    public static void HtmlFromFile(string htmlPath, Stream output)
    {
        // snippet:html-from-file
        var html = PdfHtml.FromFile(htmlPath);
        PdfOperations.ConvertHtml(html, output);
        // end
    }

    /// <summary>
    /// An image placed on a page at a chosen width; its height keeps the image's proportions.
    /// </summary>
    /// <param name="logoPng">The image's bytes.</param>
    /// <param name="output">The stream the PDF is written to.</param>
    public static void Image(byte[] logoPng, Stream output)
    {
        // snippet:add-image
        var logo = PdfImage.FromBytes(logoPng);
        var document = new PdfDocument();
        document.AddPage().AddImage(logo, 72, 700, width: 120);
        document.Save(output);
        // end
    }

    /// <summary>
    /// Two PDFs joined in one call.
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
    /// A footer with the page number and page count on every page.
    /// </summary>
    /// <param name="output">The stream the PDF is written to.</param>
    public static void PageNumbers(Stream output)
    {
        // snippet:page-numbers
        var document = new PdfDocument { MarginLeft = 40, MarginRight = 40 };
        document.Footer.Height = 24;
        document.Footer.AddText("Page {page} of {pages}", 0, 8, 9, PdfTextAlignment.Right);
        document.Body.AddText("Monthly report");
        document.Save(output);
        // end
    }

    /// <summary>
    /// Pages 2 to 4 of an existing PDF taken into a new document.
    /// </summary>
    /// <param name="reportPdf">The existing PDF's bytes.</param>
    /// <param name="output">The stream the PDF is written to.</param>
    public static void PickPages(byte[] reportPdf, Stream output)
    {
        // snippet:pick-pages
        var report = PdfFile.FromBytes(reportPdf);
        var document = new PdfDocument();
        document.AddPages(report, firstPage: 2, lastPage: 4);
        document.Save(output);
        // end
    }

    /// <summary>
    /// A table whose header row repeats at the top of every page it continues onto.
    /// </summary>
    /// <param name="output">The stream the PDF is written to.</param>
    public static void RepeatHeader(Stream output)
    {
        // snippet:repeat-header
        var document = new PdfDocument();
        var table = document.Body.AddTable(500);
        table.HeaderRowCount = 1;
        table.RepeatHeaderRows = true;
        table.SetText(0, 0, "Name");
        for (int row = 1; row <= 100; row++) table.SetText(row, 0, $"Employee {row}");
        document.Save(output);
        // end
    }

    /// <summary>
    /// A PDF split into files of two pages each.
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

    /// <summary>
    /// Text placed in millimeters.
    /// </summary>
    /// <param name="output">The stream the PDF is written to.</param>
    public static void Units(Stream output)
    {
        // snippet:units
        var document = new PdfDocument();
        double left = PdfUnits.FromMillimeters(20), top = PdfUnits.FromMillimeters(270);
        document.AddPage().AddText("20 mm from the left edge", left, top);
        document.Save(output);
        // end
    }

    /// <summary>
    /// A paragraph that wraps across the page width, added as HTML to the body flow.
    /// </summary>
    /// <param name="output">The stream the PDF is written to.</param>
    public static void WrapParagraph(Stream output)
    {
        // snippet:wrap-paragraph
        var document = new PdfDocument();
        var paragraph = PdfHtml.FromString("<p>This paragraph is long enough to need a second line, so it wraps at the body's right edge.</p>");
        document.Body.AddHtml(paragraph);
        document.Save(output);
        // end
    }

    #endregion

    #endregion
}
