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
    /// The Create sample: a table with a line of text in its first cell. It differs from the first PDF the home page
    /// shows above it (QF-26112201).
    /// </summary>
    /// <param name="output">The stream the PDF is written to.</param>
    public static void Create(Stream output)
    {
        var document = new PdfDocument();
        // snippet:create
        var table = document.Body.AddTable(500);
        table.SetColumnWidths(350, 150);
        table.AddRow().GetCell(0).Text = "Hello, table!";
        document.Save(output);
        // end
    }

    /// <summary>
    /// The Real-life documents sample: a table whose header row repeats on every page it runs onto.
    /// </summary>
    /// <param name="output">The stream the PDF is written to.</param>
    public static void Documents(Stream output)
    {
        var document = new PdfDocument();
        // snippet:documents
        var table = document.Body.AddTable(500);
        table.HeaderRowCount = 1;
        table.RepeatHeaderRows = true;
        // end
        table.SetColumnWidths(350, 150);
        var header = table.AddRow();
        header.GetCell(0).Text = "Item";
        header.GetCell(1).Text = "Amount";
        for (int i = 1; i <= 80; i++)
        {
            var row = table.AddRow();
            row.GetCell(0).Text = $"Item {i}";
            row.GetCell(1).Text = (i * 10).ToString();
        }

        document.Save(output);
    }

    /// <summary>
    /// The home page's first PDF: a page with a line of text, saved as hello.pdf in a folder.
    /// </summary>
    /// <param name="folder">The folder to write hello.pdf to.</param>
    public static void First(string folder)
    {
        // snippet:first
        var document = new PdfDocument();
        document.AddPage().AddText("Hello, PDF!", 72, 720, 24);
        document.Save(Path.Combine(folder, "hello.pdf"));
        // end
    }

    /// <summary>
    /// The HTML to PDF sample: HTML converted to a PDF in one call.
    /// </summary>
    /// <param name="output">The stream the PDF is written to.</param>
    public static void Html(Stream output)
    {
        // snippet:html
        var html = PdfHtml.FromString("<h1>Hello, <i>PDF</i>!</h1>");
        PdfOperations.ConvertHtml(html, output);
        // end
    }

    /// <summary>
    /// The Languages sample: a font for Telugu added to the fallback list, then text in two scripts.
    /// </summary>
    /// <param name="fontBytes">A font with Telugu letters, for example read from a .ttf file.</param>
    /// <param name="output">The stream the PDF is written to.</param>
    public static void Languages(byte[] fontBytes, Stream output)
    {
        // snippet:languages
        var document = new PdfDocument();
        document.FontFallback.Add(PdfFont.FromBytes(fontBytes));
        document.AddPage().AddText("Hello, నమస్తే!", 72, 720, 24);
        document.Save(output);
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
