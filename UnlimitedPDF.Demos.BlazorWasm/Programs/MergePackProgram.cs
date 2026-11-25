// Document packs: a month-end pack of documents merged behind a cover page, one bookmark each.
// A complete console program: it writes document-pack.pdf.
// It first writes the small sample PDFs it merges: invoice.pdf, receipt.pdf, and
// account-statement.pdf.
//
// Run it:
//   1. dotnet new console -n MergePackExample
//   2. cd MergePackExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. dotnet run
// Then open document-pack.pdf in the MergePackExample folder.

using UnlimitedPDF;

namespace MergePackExample;

public static class Program
{
    public static void Main()
    {
        // The sample data: the finance pack. Remove a line to leave a document out, or make an
        // employee pack ("Payslip", "Contract") or a patient pack ("Medical report", "Receipt").
        string coverTitle = "Month-end pack";
        PackDocument[] documents =
        [
            new("Invoice", "invoice.pdf"),
            new("Receipt", "receipt.pdf"),
            new("Account statement", "account-statement.pdf"),
        ];

        // This part only makes sample input files to merge; use your own PDFs instead.
        WriteSamplePdf("invoice.pdf", "Invoice", 2);
        WriteSamplePdf("receipt.pdf", "Receipt", 1);
        WriteSamplePdf("account-statement.pdf", "Account statement", 3);

        var files = documents.Select(document => PdfFile.FromFile(document.Path)).ToArray();

        // The footer shows on the built cover only; imported pages keep their own look.
        var pack = new PdfDocument();
        pack.Footer.Height = 30;
        pack.Footer.AddText("{pages} pages", 0, 10, 10, PdfTextAlignment.Center);

        var cover = pack.AddPage();
        cover.AddText(coverTitle, 72, 720, 26);
        cover.AddText("Contents", 72, 680, 14);
        int line = 655;
        for (int i = 0; i < documents.Length; i++)
        {
            int pages = files[i].PageCount;
            string label = $"{documents[i].Name} ({pages} {(pages == 1 ? "page" : "pages")})";
            cover.AddText(label, 90, line, 12);
            line -= 20;
        }

        // Each document is added under one top-level bookmark named after it.
        for (int i = 0; i < documents.Length; i++)
            pack.AddPages(files[i], bookmarkTitle: documents[i].Name);

        pack.Save("document-pack.pdf");
        Console.WriteLine($"Wrote {Path.GetFullPath("document-pack.pdf")}");
    }

    // Writes a simple sample PDF: a title and some text on each page, "Page x of y" in the footer.
    private static void WriteSamplePdf(string path, string title, int pageCount)
    {
        var document = new PdfDocument();
        document.Footer.Height = 30;
        document.Footer.AddText("Page {page} of {pages}", 0, 10, 10, PdfTextAlignment.Center);
        for (int page = 1; page <= pageCount; page++)
        {
            var pdfPage = document.AddPage();
            pdfPage.AddText(title, 72, 720, 24);
            pdfPage.AddText($"This is page {page} of the sample {title.ToLowerInvariant()}.",
                72, 680, 12);
        }
        document.Save(path);
    }

    private sealed record PackDocument(string Name, string Path);
}
