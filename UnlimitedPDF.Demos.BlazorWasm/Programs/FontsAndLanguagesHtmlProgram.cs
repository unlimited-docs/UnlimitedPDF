// Fonts and languages: an invoice in Hindi, drawn from HTML with free Noto Sans fonts.
// A complete console program: it writes fonts-and-languages-hindi.pdf.
//
// Run it:
//   1. dotnet new console -n FontsAndLanguagesHtmlExample
//   2. cd FontsAndLanguagesHtmlExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. Save these files in the FontsAndLanguagesHtmlExample folder, next to Program.cs:
//        https://unlimitedpdf.dev/fonts/NotoSans-Regular.ttf
//        https://unlimitedpdf.dev/fonts/NotoSansDevanagari-Regular.ttf
//   6. dotnet run
// Then open fonts-and-languages-hindi.pdf in the FontsAndLanguagesHtmlExample folder.

using UnlimitedPDF;

namespace FontsAndLanguagesHtmlExample;

public static class Program
{
    private static readonly System.Globalization.CultureInfo Invariant =
        System.Globalization.CultureInfo.InvariantCulture;

    public static void Main()
    {
        // The sample data: change it, run again, and open fonts-and-languages-hindi.pdf.
        // The texts below are in Hindi; change them to write the invoice in another language. For
        // another script, save its font from https://unlimitedpdf.dev/fonts/ too (for example
        // NotoSansTelugu-Regular.ttf for Telugu, NotoSansTamil-Regular.ttf for Tamil) and add it
        // to fonts under a family name. Noto Sans draws Latin, Greek, and Cyrillic text.
        (string Family, string File)[] fonts =
        [
            ("Invoice Latin", "NotoSans-Regular.ttf"),
            ("Invoice Devanagari", "NotoSansDevanagari-Regular.ttf"),
        ];
        var labels = new Labels(
            Title: "कर बीजक",
            Number: "बीजक संख्या",
            Date: "दिनांक",
            Seller: "विक्रेता",
            Buyer: "क्रेता",
            Item: "विवरण",
            Quantity: "मात्रा",
            Rate: "दर",
            Amount: "राशि",
            Total: "कुल योग",
            Thanks: "आपके व्यापार के लिए धन्यवाद!");
        string sellerName = "शर्मा वस्त्र भंडार";
        string sellerAddress = "चांदनी चौक, दिल्ली 110006";
        string buyerName = "गुप्ता ट्रेडर्स";
        string buyerAddress = "एम. आई. रोड, जयपुर 302001";
        string invoiceNumber = "2026/0042";
        DateTime invoiceDate = DateTime.Today;
        LineItem[] items =
        [
            new("सूती कुर्ता", 2, 850m),
            new("रेशमी दुपट्टा", 1, 1200m),
            new("हथकरघा शॉल", 1, 2400m),
        ];

        // The page's direction and language: set direction to "rtl" for an Arabic, Persian, Urdu,
        // Hebrew, or Dhivehi invoice, with its language tag (for example "ar"). Null leaves them
        // out.
        string? direction = null;
        string language = "hi";

        // The CSS weights of the body and the headings. A variable font draws each weight as its
        // own instance; for a font with one weight, SynthesizeMissingStyles thickens it for bold.
        int bodyWeight = 400;
        int headingWeight = 700;
        bool synthesizeMissingStyles = true;

        // With FailOnMissingCharacters, saving stops on a character no font has instead of drawing
        // it as '?'.
        bool failOnMissingCharacters = false;

        // Amounts are written in rupees, ₹ 1,500.00, the same way on every computer.
        string Money(decimal amount) => "₹ " + amount.ToString("#,##0.00", Invariant);

        // Each font is loaded once and registered under a CSS family name.
        var options = new PdfHtmlLoadOptions { SynthesizeMissingStyles = synthesizeMissingStyles };
        foreach (var (family, file) in fonts) options.Fonts.Add(family, PdfFont.FromFile(file));

        // The families are listed in order, so each character takes the first font that has it.
        string families = string.Concat(fonts.Select(f => $"'{f.Family}', ")) + "sans-serif";

        // The invoice as HTML. Every value is HTML-encoded, so it is drawn as written and never
        // adds markup.
        static string E(string text) => System.Net.WebUtility.HtmlEncode(text);
        var sb = new System.Text.StringBuilder();
        sb.Append("<style>")
          // Line height 1.6 leaves room for marks drawn above and below Indic letters.
          .Append("body { font-family: ").Append(families)
          .Append("; font-size: 10pt; line-height: 1.6; color: #1f2937; font-weight: ")
          .Append(bodyWeight.ToString(Invariant)).Append(" }")
          .Append("h1, b, .items th, .total td { font-weight: ")
          .Append(headingWeight.ToString(Invariant)).Append(" }")
          .Append(".band { background-color: #e8f5ee; border-bottom: 3pt solid #168059; ")
          .Append("padding: 14pt 18pt }")
          .Append("h1 { font-size: 22pt; margin: 0 }")
          .Append(".parties { width: 100%; margin-top: 14pt } ")
          .Append(".parties td { vertical-align: top; width: 50% }")
          .Append(".items { width: 100%; border-collapse: collapse; margin-top: 12pt }")
          .Append(".items th, .items td { border: 0.75pt solid #1f2937; padding: 4pt 6pt }")
          .Append(".items th { background-color: #d1ebdd; text-align: start }")
          .Append(".num { text-align: end } .total td { background-color: #d1ebdd }")
          .Append(".thanks { margin-top: 16pt; color: #475569 }")
          .Append("</style>");

        sb.Append("<div class=\"band\"><h1>").Append(E(labels.Title)).Append("</h1></div>");
        sb.Append("<table class=\"parties\"><tr>")
          .Append("<td><b>").Append(E(labels.Seller)).Append("</b><br>").Append(E(sellerName))
          .Append("<br>").Append(E(sellerAddress)).Append("</td>")
          .Append("<td><b>").Append(E(labels.Buyer)).Append("</b><br>").Append(E(buyerName))
          .Append("<br>").Append(E(buyerAddress)).Append("</td>")
          .Append("</tr></table>");
        sb.Append("<p>").Append(E(labels.Number)).Append(": ").Append(E(invoiceNumber))
          .Append("<br>").Append(E(labels.Date)).Append(": ")
          .Append(invoiceDate.ToString("dd/MM/yyyy", Invariant)).Append("</p>");

        sb.Append("<table class=\"items\"><thead><tr>")
          .Append("<th>").Append(E(labels.Item)).Append("</th>")
          .Append("<th class=\"num\">").Append(E(labels.Quantity)).Append("</th>")
          .Append("<th class=\"num\">").Append(E(labels.Rate)).Append("</th>")
          .Append("<th class=\"num\">").Append(E(labels.Amount)).Append("</th>")
          .Append("</tr></thead><tbody>");
        foreach (var item in items)
        {
            sb.Append("<tr><td>").Append(E(item.Description)).Append("</td>")
              .Append("<td class=\"num\">").Append(item.Quantity.ToString(Invariant))
              .Append("</td>")
              .Append("<td class=\"num\">").Append(E(Money(item.Rate))).Append("</td>")
              .Append("<td class=\"num\">").Append(E(Money(item.Amount))).Append("</td></tr>");
        }
        sb.Append("<tr class=\"total\"><td colspan=\"3\">").Append(E(labels.Total)).Append("</td>")
          .Append("<td class=\"num\">").Append(E(Money(items.Sum(item => item.Amount))))
          .Append("</td></tr>")
          .Append("</tbody></table>");
        sb.Append("<p class=\"thanks\">").Append(E(labels.Thanks)).Append("</p>");

        // A right-to-left invoice sets its direction and language on the page, as right-to-left
        // HTML does.
        if (direction is not null)
        {
            sb.Insert(0, $"<html dir=\"{direction}\" lang=\"{language}\">").Append("</html>");
        }

        var html = PdfHtml.FromString(sb.ToString(), options);

        var document = new PdfDocument { FailOnMissingCharacters = failOnMissingCharacters };
        document.Body.AddHtml(html);
        document.Save("fonts-and-languages-hindi.pdf");

        // After saving, the warnings list what was skipped or replaced.
        foreach (var warning in html.Warnings) Console.WriteLine(warning);
        Console.WriteLine($"Wrote {Path.GetFullPath("fonts-and-languages-hindi.pdf")}");
    }

    private sealed record Labels(
        string Title,
        string Number,
        string Date,
        string Seller,
        string Buyer,
        string Item,
        string Quantity,
        string Rate,
        string Amount,
        string Total,
        string Thanks);

    private sealed record LineItem(string Description, int Quantity, decimal Rate)
    {
        public decimal Amount => Quantity * Rate;
    }
}
