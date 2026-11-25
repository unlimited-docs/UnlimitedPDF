// Fonts and languages: an invoice in Hindi, drawn with the text calls and free Noto Sans fonts.
// A complete console program: it writes fonts-and-languages-hindi.pdf.
//
// Run it:
//   1. dotnet new console -n FontsAndLanguagesExample
//   2. cd FontsAndLanguagesExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. Save these files in the FontsAndLanguagesExample folder, next to Program.cs:
//        https://unlimitedpdf.dev/fonts/NotoSans-Regular.ttf
//        https://unlimitedpdf.dev/fonts/NotoSansDevanagari-Regular.ttf
//   6. dotnet run
// Then open fonts-and-languages-hindi.pdf in the FontsAndLanguagesExample folder.

using UnlimitedPDF;

namespace FontsAndLanguagesExample;

public static class Program
{
    private const double Left = 50;

    private static readonly System.Globalization.CultureInfo Invariant =
        System.Globalization.CultureInfo.InvariantCulture;

    private static readonly PdfColor BandTint = new(0.91, 0.96, 0.93);
    private static readonly PdfColor Green = new(0.09, 0.50, 0.36);
    private static readonly PdfColor HeaderTint = new(0.82, 0.92, 0.86);

    public static void Main()
    {
        // The sample data: change it, run again, and open fonts-and-languages-hindi.pdf.
        // The texts below are in Hindi; change them to write the invoice in another language. For
        // another script, save its font from https://unlimitedpdf.dev/fonts/ too (for example
        // NotoSansTelugu-Regular.ttf for Telugu, NotoSansTamil-Regular.ttf for Tamil) and put its
        // file name in fontFiles. Noto Sans draws Latin, Greek, and Cyrillic text.
        string[] fontFiles = ["NotoSans-Regular.ttf", "NotoSansDevanagari-Regular.ttf"];
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

        // Right to left for an Arabic, Persian, Urdu, Hebrew, or Dhivehi invoice; Auto takes each
        // text's first letter.
        PdfTextDirection direction = PdfTextDirection.Auto;

        // With FailOnMissingCharacters, saving stops on a character no font has instead of drawing
        // it as '?'.
        bool failOnMissingCharacters = false;

        // Amounts are written in rupees, ₹ 1,500.00, the same way on every computer.
        string Money(decimal amount) => "₹ " + amount.ToString("#,##0.00", Invariant);

        // Each font is loaded once. These fonts have one weight, so the headings use them too; a
        // variable font can be loaded again with new PdfFontLoadOptions { Weight = 700 } for them.
        var fonts = fontFiles.Select(file => PdfFont.FromFile(file)).ToList();
        var headingFonts = fonts;

        var document = new PdfDocument
        {
            MarginBottom = 24,
            FailOnMissingCharacters = failOnMissingCharacters,
        };

        // A right-to-left invoice reads from the right: text starts at the right edge, and tables
        // put their first column at the right. Left to right shows what the setting changes.
        document.TextDirection = direction;

        // No text below names a font, so each character is drawn with the first of these fonts
        // that has it: one font per script, in order. A Western European character none of them has
        // uses the built-in font.
        foreach (var font in fonts) document.FontFallback.Add(font);

        // A header band with the title, drawn with a heading font that has the title's letters.
        var (pageWidth, _) = PdfPageSize.A4.ToDimensions();
        document.Header.Height = 76;
        document.Header.AddRectangle(0, 0, pageWidth, 72, BandTint);
        document.Header.AddRectangle(0, 72, pageWidth, 4, Green);
        if (HeadingFont(labels.Title, headingFonts) is { } titleFont)
        {
            document.Header.AddText(labels.Title, titleFont, Left, 20, 22);
        }
        else
        {
            document.Header.AddText(labels.Title, Left, 20, 22);
        }

        document.Footer.Height = 26;
        document.Footer.AddText(labels.Thanks, Left, 10, 9);
        document.Footer.AddText("{page} / {pages}", Left, 10, 9, PdfTextAlignment.End);

        document.BodyMarginTop = 18;
        var body = document.Body;

        // Seller and buyer side by side, in a table without borders.
        var parties = body.AddTable(495, x: Left);
        parties.SetColumnWidths(248, 247);
        AddDetailRow(parties, labels.Seller, labels.Buyer, headingFonts);
        AddDetailRow(parties, sellerName, buyerName, null);
        AddDetailRow(parties, sellerAddress, buyerAddress, null);

        body.AddText($"{labels.Number}: {invoiceNumber}", spacing: 14, fontSize: 10, x: Left);
        string date = invoiceDate.ToString("dd/MM/yyyy", Invariant);
        body.AddText($"{labels.Date}: {date}", fontSize: 10, x: Left);

        // Line items; the header row repeats on every page.
        var table = body.AddTable(495, spacing: 12, x: Left);
        table.SetColumnWidths(195, 80, 105, 115);
        table.HeaderRowCount = 1;
        table.RepeatHeaderRows = true;
        var header = table.AddRow();
        string[] headings = [labels.Item, labels.Quantity, labels.Rate, labels.Amount];
        for (int column = 0; column < headings.Length; column++)
        {
            header.GetCell(column).Text = headings[column];
            header.GetCell(column).BackgroundColor = HeaderTint;
        }

        foreach (var item in items)
        {
            var row = table.AddRow();
            row.HeightMode = PdfRowHeightMode.AtLeast;
            row.GetCell(0).Text = item.Description;
            SetRight(row.GetCell(1), item.Quantity.ToString(Invariant));
            SetRight(row.GetCell(2), Money(item.Rate));
            SetRight(row.GetCell(3), Money(item.Amount));
        }

        var total = table.AddRow();
        total.GetCell(0).Text = labels.Total;
        SetRight(total.GetCell(3), Money(items.Sum(item => item.Amount)));
        for (int column = 0; column < 4; column++)
        {
            total.GetCell(column).BackgroundColor = HeaderTint;
        }

        document.Save("fonts-and-languages-hindi.pdf");
        Console.WriteLine($"Wrote {Path.GetFullPath("fonts-and-languages-hindi.pdf")}");
    }

    // A row of two texts without borders; a heading row uses a heading font that has its letters.
    private static void AddDetailRow(
        PdfTable table, string left, string right, IReadOnlyList<PdfFont>? headingFonts)
    {
        var row = table.AddRow();
        row.HeightMode = PdfRowHeightMode.AtLeast;
        row.Height = 16;
        string[] texts = [left, right];
        for (int column = 0; column < texts.Length; column++)
        {
            var cell = row.GetCell(column);
            if (headingFonts is not null && HeadingFont(texts[column], headingFonts) is { } font)
            {
                cell.SetContent(new PdfCellText(texts[column], font));
            }
            else
            {
                cell.Text = texts[column];
            }

            cell.Padding = 0;
            foreach (var border in new[]
                { cell.BorderLeft, cell.BorderTop, cell.BorderRight, cell.BorderBottom })
            {
                border.Color = PdfColor.White;
            }
        }
    }

    // The first heading font that has every character of the text, so a Hindi title is drawn with
    // the Devanagari font; null when none has them all, and the document's fonts draw it.
    private static PdfFont? HeadingFont(string text, IReadOnlyList<PdfFont> headingFonts)
    {
        var check = new PdfDocument();
        return headingFonts.FirstOrDefault(
            font => check.FindMissingCharacters(text, font).Count == 0);
    }

    private static void SetRight(PdfCell cell, string text)
    {
        cell.Text = text;
        cell.HorizontalAlignment = PdfCellHorizontalAlignment.End;
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
