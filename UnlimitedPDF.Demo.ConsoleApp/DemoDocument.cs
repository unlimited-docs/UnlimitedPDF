using UnlimitedPDF;

namespace UnlimitedPDF.Demo.ConsoleApp;

/// <summary>
/// Builds the demo document: an invoice in the body flow with product images, an image samples page, and a plain
/// page, each with the logo in its header and a page number in its footer.
/// </summary>
public static class DemoDocument
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the demo document from the images in <paramref name="assetsDirectory"/>.
    /// </summary>
    /// <param name="assetsDirectory">The folder holding logo.png, keyboard.jpg, dock.jpg, and cmyk-sample.jpg.</param>
    /// <returns>The document, ready to save.</returns>
    public static PdfDocument Build(string assetsDirectory)
    {
        // Load each image once; it can be placed any number of times and is stored once in the PDF.
        var logo = PdfImage.FromFile(Path.Combine(assetsDirectory, "logo.png"));
        var keyboard = PdfImage.FromFile(Path.Combine(assetsDirectory, "keyboard.jpg"));
        var dock = PdfImage.FromFile(Path.Combine(assetsDirectory, "dock.jpg"));
        var cmykSample = PdfImage.FromFile(Path.Combine(assetsDirectory, "cmyk-sample.jpg"));

        var pdfDocument = new PdfDocument { MarginBottom = 20 };

        // Every page gets the logo in its header band and a page number in its footer.
        pdfDocument.Header.Height = 80;
        pdfDocument.Header.AddImage(logo, 492, 10, width: 70);
        pdfDocument.Footer.Height = 20;
        pdfDocument.Footer.AddText("Page {page} of {pages}", 33, 4, 9, PdfTextAlignment.Right);

        // --- Page 1: Invoice, in the body flow: each part follows the one above it ---
        var body = pdfDocument.Body;

        // 1. Invoice Header
        body.AddText("INVOICE", fontSize: 20, alignment: PdfTextAlignment.Center);

        // Seller and Buyer Details, side by side in a table without borders
        var parties = body.AddTable(512, spacing: 16, x: 50);
        parties.SetColumnWidths(300, 212);
        AddDetailRow(parties, "Seller:", "Buyer:");
        AddDetailRow(parties, "ABC Electronics Pvt. Ltd.", "UnlimitedPDF");
        AddDetailRow(parties, "123 Tech Park, Bangalore, KA 560001", "456 Code Street, Hyderabad, TS 500001");
        AddDetailRow(parties, "GSTIN: 222222222222225", "");

        // Invoice Metadata
        body.AddText($"Invoice No: INV-{DateTime.Now:yyyy-MM-dd}-001", spacing: 16, fontSize: 10, x: 50);
        body.AddText($"Date: {DateTime.Now:dd-MMM-yyyy}", fontSize: 10, x: 50);

        // 2. Items Table; its header row repeats if the items continue on another page
        var itemsTable = body.AddTable(512, spacing: 14, x: 50);
        itemsTable.SetColumnWidths(40, 210, 60, 100, 102);
        itemsTable.HeaderRowCount = 1;
        itemsTable.RepeatHeaderRows = true;

        // Table Headers
        var headerRow = itemsTable.AddRow();
        headerRow.GetCell(0).Text = "S.No.";
        headerRow.GetCell(0).BackgroundColor = PdfColor.LightGray;
        headerRow.GetCell(1).Text = "Item Description";
        headerRow.GetCell(1).BackgroundColor = PdfColor.LightGray;
        headerRow.GetCell(2).Text = "Qty";
        headerRow.GetCell(2).BackgroundColor = PdfColor.LightGray;
        headerRow.GetCell(3).Text = "Rate (INR)";
        headerRow.GetCell(3).BackgroundColor = PdfColor.LightGray;
        headerRow.GetCell(4).Text = "Amount (INR)";
        headerRow.GetCell(4).BackgroundColor = PdfColor.LightGray;

        // Item rows grow to fit a product image stacked above its name.
        AddItemRow(itemsTable, "1", keyboard, "Wireless Keyboard", "2", "1250.00", "2500.00");
        AddItemRow(itemsTable, "2", dock, "USB-C Docking Station", "1", "4500.00", "4500.00");

        // 3. Totals Section and 4. Terms: a block that follows the items and stays together
        double subtotal = 7000.00;
        double cgst = subtotal * 0.09; // 9% CGST
        double sgst = subtotal * 0.09; // 9% SGST
        double total = subtotal + cgst + sgst;

        var closing = body.AddBlock(spacing: 16);
        var totalsTable = closing.AddTable(212, x: 350);
        totalsTable.SetColumnWidths(110, 102);

        totalsTable.GetOrCreateCell(0, 0).Text = "Subtotal";
        var subtotalValueCell = totalsTable.GetOrCreateCell(0, 1);
        subtotalValueCell.Text = subtotal.ToString("F2");
        subtotalValueCell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;

        totalsTable.GetOrCreateCell(1, 0).Text = "CGST @ 9%";
        var cgstValueCell = totalsTable.GetOrCreateCell(1, 1);
        cgstValueCell.Text = cgst.ToString("F2");
        cgstValueCell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;

        totalsTable.GetOrCreateCell(2, 0).Text = "SGST @ 9%";
        var sgstValueCell = totalsTable.GetOrCreateCell(2, 1);
        sgstValueCell.Text = sgst.ToString("F2");
        sgstValueCell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;

        var totalCell = totalsTable.GetOrCreateCell(3, 0);
        totalCell.Text = "Total";
        totalCell.BackgroundColor = PdfColor.LightGray;

        var totalValueCell = totalsTable.GetOrCreateCell(3, 1);
        totalValueCell.Text = total.ToString("F2");
        totalValueCell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;
        totalValueCell.BackgroundColor = PdfColor.LightGray;

        closing.AddText("Terms & Conditions:", spacing: 20, fontSize: 10, x: 50);
        closing.AddText("1. Payment due within 30 days.", spacing: 2, fontSize: 9, x: 50);
        closing.AddText("2. Goods once sold will not be taken back.", spacing: 2, fontSize: 9, x: 50);

        // --- Page 2: Image samples ---
        // AddPage also starts a new page in the flow; this page places its content at page coordinates.
        var page2 = pdfDocument.AddPage();
        page2.AddText("Image samples", 50, 790, 16);

        // No size: 96 pixels per inch, so 96 x 64 px draws 72 x 48 pt.
        page2.AddText("Natural size", 50, 740, 10);
        page2.AddImage(keyboard, 50, 680);

        // One size: the other follows the aspect ratio.
        page2.AddText("Width only (2 in)", 200, 740, 10);
        page2.AddImage(dock, 200, 620, width: PdfUnits.FromInches(2));

        // Both sizes: stretch fills the box, contain fits inside it centered.
        page2.AddText("Stretch to 150 x 50 pt", 50, 590, 10);
        page2.AddImage(keyboard, 50, 530, 150, 50);
        page2.AddText("Contain in 150 x 50 pt", 250, 590, 10);
        page2.AddImage(keyboard, 250, 530, 150, 50, new PdfImagePlacementOptions { Fit = PdfImageFit.Contain });

        // A CMYK JPEG with an Adobe marker should show cyan, magenta, yellow, and black, not their negatives.
        page2.AddText("CMYK JPEG: cyan, magenta / yellow, black", 50, 500, 10);
        page2.AddImage(cmykSample, 50, 390, width: PdfUnits.FromMillimeters(40));

        // Vertical alignment of a cell's content block: the image and caption (62.4 pt) move within a 110 pt row.
        var alignmentTable = page2.AddTable(50, 360, 450);
        alignmentTable.SetColumnWidths(150, 150, 150);
        var alignmentRow = alignmentTable.AddRow();
        alignmentRow.Height = 110;
        var alignments = new[] { PdfCellVerticalAlignment.Top, PdfCellVerticalAlignment.Middle, PdfCellVerticalAlignment.Bottom };
        for (int c = 0; c < alignments.Length; c++)
        {
            var cell = alignmentRow.GetCell(c);
            cell.SetContent(dock, $"VAlign {alignments[c]}");
            cell.VerticalAlignment = alignments[c];
            cell.HorizontalAlignment = PdfCellHorizontalAlignment.Center;
        }

        // --- Page 3: Other Content ---
        var page3 = pdfDocument.AddPage();
        page3.AddText("This is the first line on the third page.", 50, 750, 12);

        return pdfDocument;
    }

    #endregion

    #region Private Methods

    private static void AddItemRow(PdfTable table, string number, PdfImage photo, string description, string quantity, string rate, string amount)
    {
        var row = table.AddRow();
        row.HeightMode = PdfRowHeightMode.AtLeast;
        row.GetCell(0).Text = number;
        row.GetCell(1).SetContent(photo, description);
        row.GetCell(2).Text = quantity;
        row.GetCell(2).HorizontalAlignment = PdfCellHorizontalAlignment.Right;
        row.GetCell(3).Text = rate;
        row.GetCell(3).HorizontalAlignment = PdfCellHorizontalAlignment.Right;
        row.GetCell(4).Text = amount;
        row.GetCell(4).HorizontalAlignment = PdfCellHorizontalAlignment.Right;
    }

    // One row of label or value text without borders: cell borders take the white background.
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

    #endregion

    #endregion
}
