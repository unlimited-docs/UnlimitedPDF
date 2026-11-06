using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Delivery note: the paper that travels with a shipment. The item table repeats its header row on every page, and
/// the "Received by" block stays together after the last item so the recipient signs one place.
/// </summary>
public static class DeliveryNoteSample
{
    #region Fields and Constants

    private const double Width = 495;

    private static readonly PdfColor _gray = new(0.27, 0.29, 0.33);
    private static readonly PdfColor _headerTint = new(0.95, 0.89, 0.88);
    private static readonly PdfColor _red = new(0.70, 0.15, 0.12);
    private static readonly PdfColor _shortTint = new(1.0, 0.95, 0.80);

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the document.
    /// </summary>
    /// <param name="input">The delivery data.</param>
    /// <param name="images">The carrier's logo ("delivery-note-logo.png").</param>
    /// <returns>The document, ready to save.</returns>
    public static PdfDocument Build(DeliveryNoteInput input, SampleImages images)
    {
        var document = new PdfDocument { MarginTop = 32, MarginBottom = 24, MarginLeft = 50, MarginRight = 50 };

        document.Header.Height = 62;
        document.Header.AddImage(images["delivery-note-logo.png"], 0, 0, 150, 50, new PdfImagePlacementOptions { Fit = PdfImageFit.Contain });
        document.Header.AddText("DELIVERY NOTE", 0, 6, 20, PdfTextAlignment.Right);
        document.Header.AddText($"{input.NoteNumber}  |  Order {input.OrderNumber}  |  {input.DeliveryDate:dd MMM yyyy}", 0, 34, 9, PdfTextAlignment.Right);
        document.Header.AddRectangle(0, 58, Width, 2, _red);
        document.Footer.Height = 22;
        document.Footer.AddRectangle(0, 0, Width, 0.5, _gray);
        document.Footer.AddText($"Delivery note {input.NoteNumber}. Check the goods before signing.", 0, 8, 8);
        document.Footer.AddText("Page {page} of {pages}", 0, 8, 8, PdfTextAlignment.Right);
        document.BodyMarginTop = 18;
        document.BodyMarginBottom = 14;
        var body = document.Body;

        // Sender and recipient side by side, in a table without borders. Each typed address line is a row; table cells
        // do not wrap text, so a line wider than its column (about 40 characters at 12 pt) runs into the next column.
        var parties = body.AddTable(Width);
        parties.SetColumnWidths(250, 245);
        AddDetailRow(parties, "From", "Deliver to");
        AddDetailRow(parties, "Meridian Freight", input.RecipientName);
        string[] sender = AddressLines(input.SenderAddress);
        string[] recipient = AddressLines(input.RecipientAddress);
        for (int line = 0; line < Math.Max(sender.Length, recipient.Length); line++)
            AddDetailRow(parties, line < sender.Length ? sender[line] : "", line < recipient.Length ? recipient[line] : "");

        var items = body.AddTable(Width, spacing: 16);
        items.SetColumnWidths(75, 260, 80, 80);
        items.HeaderRowCount = 1;
        items.RepeatHeaderRows = true;
        var header = items.AddRow();
        string[] headings = ["Item code", "Description", "Ordered", "Shipped"];
        for (int column = 0; column < headings.Length; column++)
        {
            header.GetCell(column).Text = headings[column];
            header.GetCell(column).BackgroundColor = _headerTint;
        }

        int ordered = 0, shipped = 0;
        foreach (var line in Lines(input))
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
                for (int column = 0; column < 4; column++) row.GetCell(column).BackgroundColor = _shortTint;
            }
        }

        // The receipt block stays together after the last item.
        var receipt = body.AddBlock(spacing: 16);
        receipt.AddText($"Total ordered: {ordered}    Total shipped: {shipped}", fontSize: 10, alignment: PdfTextAlignment.Right);
        receipt.AddRectangle(Width, 1, _gray, spacing: 10);
        receipt.AddText("Received in good condition", spacing: 10, fontSize: 11);
        receipt.AddText("Received by: ______________________", spacing: 18, fontSize: 10);
        receipt.AddText("Date: ______________          Signature: ______________________", spacing: 18, fontSize: 10);

        return document;
    }

    #endregion

    #region Private Methods

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
            foreach (var border in new[] { cell.BorderLeft, cell.BorderTop, cell.BorderRight, cell.BorderBottom })
            {
                border.Color = PdfColor.White;
            }
        }
    }

    // The non-empty lines of a multi-line form field.
    private static string[] AddressLines(string text) =>
        text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();

    // The listed items, then generated ones with fictional, repeatable values.
    private static IEnumerable<DeliveryLine> Lines(DeliveryNoteInput input)
    {
        foreach (var line in input.Items) yield return line;
        int generated = Math.Clamp(input.GeneratedLines, 0, 500);
        for (int i = 1; i <= generated; i++)
        {
            int quantity = i % 12 + 1;
            yield return new DeliveryLine { Code = $"SKU-{2000 + i}", Description = $"Packaged goods, lot {100 + i}", Ordered = quantity, Shipped = i % 17 == 0 ? quantity - 1 : quantity };
        }
    }

    private static void Right(PdfCell cell, string text)
    {
        cell.Text = text;
        cell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;
    }

    #endregion

    #endregion
}
