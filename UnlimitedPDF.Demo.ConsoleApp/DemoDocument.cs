using UnlimitedPDF;
using UnlimitedPDF.Enums;
using UnlimitedPDF.Models.Table;

namespace UnlimitedPDF.Demo.ConsoleApp;

/// <summary>
/// Builds the demo document: an invoice with a logo and product images, an image samples page, and a plain page.
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

        var pdfDocument = new PdfDocument();

        // --- Page 1: Invoice ---
        var page1 = pdfDocument.AddPage();
        AddLogo(page1, logo);

        // 1. Invoice Header
        page1.AddText("INVOICE", 250, 800, 20);

        // Seller and Buyer Details
        page1.AddText("Seller:", 50, 760, 10);
        page1.AddText("ABC Electronics Pvt. Ltd.", 50, 748, 10);
        page1.AddText("123 Tech Park, Bangalore, KA 560001", 50, 736, 10);
        page1.AddText("GSTIN: 222222222222225", 50, 724, 10);

        page1.AddText("Buyer:", 350, 760, 10);
        page1.AddText("UnlimitedPDF", 350, 748, 10);
        page1.AddText("456 Code Street, Hyderabad, TS 500001", 350, 736, 10);

        // Invoice Metadata
        page1.AddText($"Invoice No: INV-{DateTime.Now:yyyy-MM-dd}-001", 50, 690, 10);
        page1.AddText($"Date: {DateTime.Now:dd-MMM-yyyy}", 50, 678, 10);

        // 2. Items Table
        var itemsTable = page1.AddTable(50, 650, 512);
        itemsTable.SetColumnWidths(40, 210, 60, 100, 102);

        // Table Headers
        var headerRow = itemsTable.AddRow();
        headerRow.GetCell(0).Text = "S.No.";
        headerRow.GetCell(0).Background = PdfColor.LightGray;
        headerRow.GetCell(1).Text = "Item Description";
        headerRow.GetCell(1).Background = PdfColor.LightGray;
        headerRow.GetCell(2).Text = "Qty";
        headerRow.GetCell(2).Background = PdfColor.LightGray;
        headerRow.GetCell(3).Text = "Rate (INR)";
        headerRow.GetCell(3).Background = PdfColor.LightGray;
        headerRow.GetCell(4).Text = "Amount (INR)";
        headerRow.GetCell(4).Background = PdfColor.LightGray;

        // Item rows grow to fit a product image stacked above its name.
        AddItemRow(itemsTable, "1", keyboard, "Wireless Keyboard", "2", "1250.00", "2500.00");
        AddItemRow(itemsTable, "2", dock, "USB-C Docking Station", "1", "4500.00", "4500.00");

        // 3. Totals Section (placed below the taller item rows)
        double subtotal = 7000.00;
        double cgst = subtotal * 0.09; // 9% CGST
        double sgst = subtotal * 0.09; // 9% SGST
        double total = subtotal + cgst + sgst;

        var totalsTable = page1.AddTable(350, 470, 212);
        totalsTable.SetColumnWidths(110, 102);

        totalsTable.GetOrCreateCell(0, 0).Text = "Subtotal";
        var subtotalValueCell = totalsTable.GetOrCreateCell(0, 1);
        subtotalValueCell.Text = subtotal.ToString("F2");
        subtotalValueCell.HAlign = PdfCellHorizontalAlignment.Right;

        totalsTable.GetOrCreateCell(1, 0).Text = "CGST @ 9%";
        var cgstValueCell = totalsTable.GetOrCreateCell(1, 1);
        cgstValueCell.Text = cgst.ToString("F2");
        cgstValueCell.HAlign = PdfCellHorizontalAlignment.Right;

        totalsTable.GetOrCreateCell(2, 0).Text = "SGST @ 9%";
        var sgstValueCell = totalsTable.GetOrCreateCell(2, 1);
        sgstValueCell.Text = sgst.ToString("F2");
        sgstValueCell.HAlign = PdfCellHorizontalAlignment.Right;

        var totalCell = totalsTable.GetOrCreateCell(3, 0);
        totalCell.Text = "Total";
        totalCell.Background = PdfColor.LightGray;

        var totalValueCell = totalsTable.GetOrCreateCell(3, 1);
        totalValueCell.Text = total.ToString("F2");
        totalValueCell.HAlign = PdfCellHorizontalAlignment.Right;
        totalValueCell.Background = PdfColor.LightGray;

        // 4. Terms and Conditions
        page1.AddText("Terms & Conditions:", 50, 370, 10);
        page1.AddText("1. Payment due within 30 days.", 50, 358, 9);
        page1.AddText("2. Goods once sold will not be taken back.", 50, 346, 9);

        // --- Page 2: Image samples ---
        var page2 = pdfDocument.AddPage();
        AddLogo(page2, logo);
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
            cell.VAlign = alignments[c];
            cell.HAlign = PdfCellHorizontalAlignment.Center;
        }

        // --- Page 3: Other Content ---
        var page3 = pdfDocument.AddPage();
        AddLogo(page3, logo);
        page3.AddText("This is the first line on the third page.", 50, 750, 12);

        return pdfDocument;
    }

    #endregion

    #region Private Methods

    private static void AddItemRow(PdfTable table, string number, PdfImage photo, string description, string quantity, string rate, string amount)
    {
        var row = table.AddRow();
        row.HeightRule = PdfRowHeightRule.AtLeast;
        row.GetCell(0).Text = number;
        row.GetCell(1).SetContent(photo, description);
        row.GetCell(2).Text = quantity;
        row.GetCell(2).HAlign = PdfCellHorizontalAlignment.Right;
        row.GetCell(3).Text = rate;
        row.GetCell(3).HAlign = PdfCellHorizontalAlignment.Right;
        row.GetCell(4).Text = amount;
        row.GetCell(4).HAlign = PdfCellHorizontalAlignment.Right;
    }

    // The logo sits in the top-right corner of every page.
    private static void AddLogo(PdfPageBuilder page, PdfImage logo)
    {
        page.AddImage(logo, 492, 762, width: 70);
    }

    #endregion

    #endregion
}
