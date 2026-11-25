// Delivery note: ordered and shipped quantities, with the signature block kept together.
// A complete console program: it writes delivery-note.pdf.
//
// Run it:
//   1. dotnet new console -n DeliveryNoteExample
//   2. cd DeliveryNoteExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. Save these images in the DeliveryNoteExample folder, next to Program.cs:
//        https://unlimitedpdf.dev/images/delivery-note-logo.png
//   6. dotnet run
// Then open delivery-note.pdf in the DeliveryNoteExample folder.

using UnlimitedPDF;

namespace DeliveryNoteExample;

public static class Program
{
    private const double Width = 495;

    private static readonly PdfColor Gray = new(0.27, 0.29, 0.33);
    private static readonly PdfColor HeaderTint = new(0.95, 0.89, 0.88);
    private static readonly PdfColor Red = new(0.70, 0.15, 0.12);
    private static readonly PdfColor ShortTint = new(1.0, 0.95, 0.80);

    public static void Main()
    {
        // The sample data: change it, run again, and open delivery-note.pdf.
        // All names and numbers are fictional. Addresses have one address line per text line.
        string noteNumber = "DN-2026-3307";
        string orderNumber = "SO-88214";
        DateTime deliveryDate = DateTime.Today;
        string senderAddress = "Gate 3, Port Road\nMumbai, MH 400001";
        string recipientName = "Central Distribution Hub";
        string recipientAddress = "Unit 7, Logistics Park\nNagpur, MH 440023";
        DeliveryLine[] lines =
        [
            new("PAL-120", "Euro pallet, timber", 40, 40),
            new("WRP-500", "Stretch wrap, 500 mm roll", 24, 20),
            new("BOX-L", "Carton box, large", 300, 300),
        ];
        // Generated lines added after the listed ones (0 to 500): raise it to see the note run
        // onto more pages.
        int generatedLines = 0;

        var document = new PdfDocument
        {
            MarginTop = 32,
            MarginBottom = 24,
            MarginLeft = 50,
            MarginRight = 50,
        };

        document.Header.Height = 62;
        var contain = new PdfImagePlacementOptions { Fit = PdfImageFit.Contain };
        document.Header.AddImage(
            PdfImage.FromFile("delivery-note-logo.png"), 0, 0, 150, 50, contain);
        document.Header.AddText("DELIVERY NOTE", 0, 6, 20, PdfTextAlignment.Right);
        document.Header.AddText(
            $"{noteNumber}  |  Order {orderNumber}  |  {deliveryDate:dd MMM yyyy}",
            0, 34, 9, PdfTextAlignment.Right);
        document.Header.AddRectangle(0, 58, Width, 2, Red);
        document.Footer.Height = 22;
        document.Footer.AddRectangle(0, 0, Width, 0.5, Gray);
        document.Footer.AddText(
            $"Delivery note {noteNumber}. Check the goods before signing.", 0, 8, 8);
        document.Footer.AddText("Page {page} of {pages}", 0, 8, 8, PdfTextAlignment.Right);
        document.BodyMarginTop = 18;
        document.BodyMarginBottom = 14;
        var body = document.Body;

        // Sender and recipient side by side, in a table without borders. Each typed address line
        // is a row; table cells do not wrap text, so a line wider than its column (about 40
        // characters at 12 pt) runs into the next column.
        var parties = body.AddTable(Width);
        parties.SetColumnWidths(250, 245);
        AddDetailRow(parties, "From", "Deliver to");
        AddDetailRow(parties, "Meridian Freight", recipientName);
        string[] sender = AddressLines(senderAddress);
        string[] recipient = AddressLines(recipientAddress);
        for (int line = 0; line < Math.Max(sender.Length, recipient.Length); line++)
        {
            AddDetailRow(
                parties,
                line < sender.Length ? sender[line] : "",
                line < recipient.Length ? recipient[line] : "");
        }

        var items = body.AddTable(Width, spacing: 16);
        items.SetColumnWidths(75, 260, 80, 80);
        items.HeaderRowCount = 1;
        items.RepeatHeaderRows = true;
        var header = items.AddRow();
        string[] headings = ["Item code", "Description", "Ordered", "Shipped"];
        for (int column = 0; column < headings.Length; column++)
        {
            header.GetCell(column).Text = headings[column];
            header.GetCell(column).BackgroundColor = HeaderTint;
        }

        int ordered = 0, shipped = 0;
        foreach (var line in AllLines(lines, generatedLines))
        {
            ordered += line.Ordered;
            shipped += line.Shipped;
            var row = items.AddRow();
            row.GetCell(0).Text = line.Code;
            row.GetCell(1).Text = line.Description;
            Right(row.GetCell(2), line.Ordered.ToString());
            Right(row.GetCell(3), line.Shipped.ToString());

            // Short-shipped lines are tinted so the recipient sees them at a glance.
            if (line.Shipped < line.Ordered)
            {
                for (int column = 0; column < 4; column++)
                {
                    row.GetCell(column).BackgroundColor = ShortTint;
                }
            }
        }

        // The receipt block stays together after the last item.
        var receipt = body.AddBlock(spacing: 16);
        receipt.AddText(
            $"Total ordered: {ordered}    Total shipped: {shipped}",
            fontSize: 10, alignment: PdfTextAlignment.Right);
        receipt.AddRectangle(Width, 1, Gray, spacing: 10);
        receipt.AddText("Received in good condition", spacing: 10, fontSize: 11);
        receipt.AddText("Received by: ______________________", spacing: 18, fontSize: 10);
        receipt.AddText(
            "Date: ______________          Signature: ______________________",
            spacing: 18, fontSize: 10);

        document.Save("delivery-note.pdf");
        Console.WriteLine($"Wrote {Path.GetFullPath("delivery-note.pdf")}");
    }

    // One row of text without borders: cell borders take the white background.
    private static void AddDetailRow(PdfTable table, string left, string right)
    {
        var row = table.AddRow();
        row.Height = 16;
        string[] texts = [left, right];
        for (int column = 0; column < texts.Length; column++)
        {
            var cell = row.GetCell(column);
            cell.Text = texts[column];
            cell.Padding = 0;
            foreach (var border in new[]
                { cell.BorderLeft, cell.BorderTop, cell.BorderRight, cell.BorderBottom })
            {
                border.Color = PdfColor.White;
            }
        }
    }

    // The non-empty lines of a multi-line address.
    private static string[] AddressLines(string text) =>
        text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();

    // The listed items, then generated ones with fictional, repeatable values.
    private static IEnumerable<DeliveryLine> AllLines(DeliveryLine[] lines, int generatedLines)
    {
        foreach (var line in lines)
        {
            yield return line;
        }

        int generated = Math.Clamp(generatedLines, 0, 500);
        for (int i = 1; i <= generated; i++)
        {
            int quantity = i % 12 + 1;
            yield return new DeliveryLine(
                $"SKU-{2000 + i}",
                $"Packaged goods, lot {100 + i}",
                quantity,
                i % 17 == 0 ? quantity - 1 : quantity);
        }
    }

    private static void Right(PdfCell cell, string text)
    {
        cell.Text = text;
        cell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;
    }

    private sealed record DeliveryLine(string Code, string Description, int Ordered, int Shipped);
}
