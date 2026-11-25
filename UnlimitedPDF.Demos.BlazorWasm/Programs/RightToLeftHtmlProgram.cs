// Right-to-left documents: an Arabic invoice that reads from the right, drawn from HTML.
// A complete console program: it writes right-to-left-arabic.pdf.
//
// Run it:
//   1. dotnet new console -n RightToLeftHtmlExample
//   2. cd RightToLeftHtmlExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. Save these files in the RightToLeftHtmlExample folder, next to Program.cs:
//        https://unlimitedpdf.dev/fonts/NotoSans-Regular.ttf
//        https://unlimitedpdf.dev/fonts/NotoSansArabic-Regular.ttf
//   6. dotnet run
// Then open right-to-left-arabic.pdf in the RightToLeftHtmlExample folder.

using UnlimitedPDF;

namespace RightToLeftHtmlExample;

public static class Program
{
    private static readonly System.Globalization.CultureInfo Invariant =
        System.Globalization.CultureInfo.InvariantCulture;

    public static void Main()
    {
        // The sample data: change it, run again, and open right-to-left-arabic.pdf.
        // The texts below are in Arabic; change them to write the invoice in another language.
        // Persian and Urdu use the same Arabic font. For Hebrew or Dhivehi, save that script's font
        // from https://unlimitedpdf.dev/fonts/ too (NotoSansHebrew-Regular.ttf or
        // NotoSansThaana-Regular.ttf), add it to fonts under a family name, and set language to
        // "he" or "dv". Noto Sans draws Latin text, such as an English name.
        (string Family, string File)[] fonts =
        [
            ("Invoice Latin", "NotoSans-Regular.ttf"),
            ("Invoice Arabic", "NotoSansArabic-Regular.ttf"),
        ];
        var labels = new Labels(
            Title: "فاتورة ضريبية",
            Number: "رقم الفاتورة",
            Date: "التاريخ",
            Seller: "البائع",
            Buyer: "المشتري",
            Item: "الوصف",
            Quantity: "الكمية",
            Rate: "السعر",
            Amount: "المبلغ",
            Total: "الإجمالي",
            Thanks: "شكراً لتعاملكم معنا!");
        string sellerName = "مؤسسة النخيل للتمور";
        string sellerAddress = "شارع الملك فهد، الرياض 12211";
        string buyerName = "مقهى الواحة";
        string buyerAddress = "شارع الكورنيش، جدة 23521";
        string invoiceNumber = "2026/0042";
        DateTime invoiceDate = DateTime.Today;
        LineItem[] items =
        [
            new("تمر سكري", 5, 45.00m),
            new("قهوة عربية", 2, 60.00m),
            new("عسل سدر", 1, 150.00m),
        ];

        // The page's direction and language: "rtl" starts each line at the right edge, "auto"
        // takes each text's first letter, and "ltr" shows what the setting changes.
        string? direction = "rtl";
        string language = "ar";

        // The CSS weights of the body and the headings. A variable font draws each weight as its
        // own instance; for a font with one weight, SynthesizeMissingStyles thickens it for bold.
        int bodyWeight = 400;
        int headingWeight = 700;
        bool synthesizeMissingStyles = true;

        // With FailOnMissingCharacters, saving stops on a character no font has instead of drawing
        // it as '?'.
        bool failOnMissingCharacters = false;

        // Amounts are written in riyals, 1,500.00 ر.س, the same way on every computer.
        string Money(decimal amount) => amount.ToString("#,##0.00", Invariant) + " ر.س";

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
        document.Save("right-to-left-arabic.pdf");

        // After saving, the warnings list what was skipped or replaced.
        foreach (var warning in html.Warnings) Console.WriteLine(warning);
        Console.WriteLine($"Wrote {Path.GetFullPath("right-to-left-arabic.pdf")}");
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
