// HTML to PDF: HTML with its images, page numbers in the footer, and headings as bookmarks.
// A complete console program: it writes html-to-pdf.pdf.
//
// Run it:
//   1. dotnet new console -n HtmlToPdfExample
//   2. cd HtmlToPdfExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. Save these files in the HtmlToPdfExample folder, next to Program.cs:
//        https://unlimitedpdf.dev/images/invoice-logo.png (save it as logo.png)
//        https://unlimitedpdf.dev/images/unlimitedpdf-demos-hero-800.jpg (save it as banner.png)
//      Optional, for HTML with Arabic or Hebrew letters, save these fonts there too:
//        https://unlimitedpdf.dev/fonts/NotoSansArabic-Regular.ttf
//        https://unlimitedpdf.dev/fonts/NotoSansHebrew-Regular.ttf
//   6. dotnet run
// Then open html-to-pdf.pdf in the HtmlToPdfExample folder.

using UnlimitedPDF;

namespace HtmlToPdfExample;

public static class Program
{
    public static void Main()
    {
        // The sample data: change it, run again, and open html-to-pdf.pdf.
        string html = """
            <!DOCTYPE html>
            <html><head><meta charset="utf-8"><title>Invoice INV-1042</title>
            <style>
              body { font-family: Helvetica, Arial, sans-serif; font-size: 10pt; color: #1f2937;
                margin: 0 }
              h1 { color: #002A73; font-size: 22pt; margin: 0 }
              .muted { color: #6b7280 }
              table.items { width: 100%; border-collapse: collapse; margin-top: 16pt }
              table.items th { background-color: #002A73; color: white; text-align: left;
                padding: 6pt }
              table.items td { border-bottom: 1px solid #d1d5db; padding: 6pt; vertical-align: top }
              .num { text-align: right }
              .total td { font-weight: bold; border-bottom: 2px solid #002A73 }
              ul.options { margin: 4pt 0 0; padding-left: 14pt; color: #6b7280; font-size: 9pt }
              @media print { .web-only { display: none } }
            </style></head>
            <body>
              <p class="web-only">Having trouble reading this invoice? Open it in your account.</p>
              <table width="100%"><tr>
                <td><img src="logo.png" width="120" alt="Northwind Supplies"></td>
                <td class="num"><h1>INVOICE</h1>
                  <div class="muted">INV-1042 &middot; 10 Nov 2026</div></td>
              </tr></table>
              <table width="100%" style="margin-top: 14pt"><tr>
                <td><b>Bill to</b><br>Acme Café Ltd<br>12 Harbour Street<br>Bristol BS1 4XE</td>
                <td><b>Ship to</b><br>Acme Café Ltd &ndash; Warehouse<br>
                  Unit 7, Dock Road<br>Bristol BS2 0QT</td>
                <td class="num"><b>Due</b><br>30 Nov 2026<br><span class="muted">Net 20</span></td>
              </tr></table>
              <table class="items">
                <thead><tr><th>Item</th><th class="num">Qty</th><th class="num">Unit</th>
                  <th class="num">Amount</th></tr></thead>
                <tbody>
                  <tr><td>Espresso machine <ul class="options"><li>Dual boiler</li>
                    <li>Stainless finish</li></ul></td>
                    <td class="num">1</td><td class="num">€1,450.00</td>
                    <td class="num">€1,450.00</td></tr>
                  <tr><td>Burr grinder</td><td class="num">2</td><td class="num">€320.00</td>
                    <td class="num">€640.00</td></tr>
                  <tr><td>Milk jugs, 600 ml</td><td class="num">6</td><td class="num">€14.50</td>
                    <td class="num">€87.00</td></tr>
                  <tr><td>Installation and training <span class="muted">(half day)</span></td>
                    <td class="num">1</td><td class="num">€180.00</td>
                    <td class="num">€180.00</td></tr>
                  <tr><td colspan="3" class="num">Subtotal</td><td class="num">€2,357.00</td></tr>
                  <tr><td colspan="3" class="num">VAT 20%</td><td class="num">€471.40</td></tr>
                  <tr class="total"><td colspan="3" class="num">Total due</td>
                    <td class="num">€2,828.40</td></tr>
                </tbody>
              </table>
              <p style="margin-top: 18pt"><b>Payment:</b> bank transfer to IBAN
                GB00&nbsp;NWBK&nbsp;0000&nbsp;0000&nbsp;0000&nbsp;00, reference INV-1042.
                Questions about this invoice:
                <a href="mailto:accounts@example.com">accounts@example.com</a>.</p>
              <p class="muted" style="font-size: 8pt">Northwind Supplies &middot; 1 Market Way,
                London &middot; VAT GB 000 0000 00</p>
            </body></html>
            """;

        // Images the HTML names, such as logo.png, are read from this folder. The library itself
        // never uses the network.
        var options = new PdfHtmlLoadOptions { BaseFolder = Directory.GetCurrentDirectory() };
        var loaded = PdfHtml.FromString(html, options);

        var document = new PdfDocument
        {
            MarginTop = 36,
            MarginBottom = 36,
            MarginLeft = 36,
            MarginRight = 36,
        };

        // Fonts for characters the standard fonts don't have: Arabic and Hebrew letters. Each is
        // used when the HTML has letters of its script and its file is in this folder.
        (string File, char First, char Last)[] fallbackFonts =
        [
            ("NotoSansArabic-Regular.ttf", '\u0600', '\u06FF'),
            ("NotoSansHebrew-Regular.ttf", '\u0590', '\u05FF'),
        ];
        foreach (var (file, first, last) in fallbackFonts)
        {
            if (File.Exists(file) && html.Any(c => c >= first && c <= last))
            {
                document.FontFallback.Add(PdfFont.FromFile(file));
            }
        }

        document.Footer.Height = 18;
        document.Footer.AddText("Page {page} of {pages}", 0, 6, 8, PdfTextAlignment.Right);
        document.Body.AddHtml(loaded, headingBookmarks: true);

        // Laying the document out now fills in the layout warnings, such as clipped content.
        _ = document.Body.RemainingHeight;
        foreach (var warning in loaded.Warnings) Console.WriteLine(warning);

        document.Save("html-to-pdf.pdf");
        Console.WriteLine($"Wrote {Path.GetFullPath("html-to-pdf.pdf")}");
    }
}
