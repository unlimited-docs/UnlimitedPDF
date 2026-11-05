using UnlimitedPDF.Enums;
using UnlimitedPDF.Models.Table;

namespace UnlimitedPDF.Tests.Support;

/// <summary>
/// Builds the documents without images whose output must stay byte-identical across the image feature
/// (AC-14). Each builder uses only API that existed before it, with fixed content.
/// </summary>
internal static class GoldenDocuments
{
    #region Fields and Constants

    public const string Invoice = "invoice";
    public const string MiddleTable = "middle-table";
    public const string PaginatedTable = "paginated-table";
    public const string TextPages = "text-pages";

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the named document and returns the bytes <see cref="PdfDocument.Save(Stream)"/> writes.
    /// Numbers are written under the invariant culture, the reference for AC-14.
    /// </summary>
    public static byte[] Build(string name)
    {
        using var culture = new CultureScope("");
        var document = name switch
        {
            Invoice => BuildInvoice(),
            MiddleTable => BuildMiddleTable(),
            PaginatedTable => BuildPaginatedTable(),
            TextPages => BuildTextPages(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown golden document.")
        };

        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    #endregion

    #region Private Methods

    // Mirrors the demo invoice (UnlimitedPDF.Demo.ConsoleApp/Program.cs at e382ac0) with fixed dates.
    private static PdfDocument BuildInvoice()
    {
        var pdfDocument = new PdfDocument();
        var page1 = pdfDocument.AddPage();

        page1.AddText("INVOICE", 250, 800, 20);

        page1.AddText("Seller:", 50, 760, 10);
        page1.AddText("ABC Electronics Pvt. Ltd.", 50, 748, 10);
        page1.AddText("123 Tech Park, Bangalore, KA 560001", 50, 736, 10);
        page1.AddText("GSTIN: 222222222222225", 50, 724, 10);

        page1.AddText("Buyer:", 350, 760, 10);
        page1.AddText("UnlimitedPDF", 350, 748, 10);
        page1.AddText("456 Code Street, Hyderabad, TS 500001", 350, 736, 10);

        page1.AddText("Invoice No: INV-2026-11-01-001", 50, 690, 10);
        page1.AddText("Date: 01-Nov-2026", 50, 678, 10);

        var itemsTable = page1.AddTable(50, 650, 512);
        itemsTable.SetColumnWidths(40, 210, 60, 100, 102);

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

        var item1Row = itemsTable.AddRow();
        item1Row.GetCell(0).Text = "1";
        item1Row.GetCell(1).Text = "Wireless Keyboard";
        item1Row.GetCell(2).Text = "2";
        item1Row.GetCell(2).HAlign = PdfCellHorizontalAlignment.Right;
        item1Row.GetCell(3).Text = "1250.00";
        item1Row.GetCell(3).HAlign = PdfCellHorizontalAlignment.Right;
        item1Row.GetCell(4).Text = "2500.00";
        item1Row.GetCell(4).HAlign = PdfCellHorizontalAlignment.Right;

        var item2Row = itemsTable.AddRow();
        item2Row.GetCell(0).Text = "2";
        item2Row.GetCell(1).Text = "USB-C Docking Station";
        item2Row.GetCell(2).Text = "1";
        item2Row.GetCell(2).HAlign = PdfCellHorizontalAlignment.Right;
        item2Row.GetCell(3).Text = "4500.00";
        item2Row.GetCell(3).HAlign = PdfCellHorizontalAlignment.Right;
        item2Row.GetCell(4).Text = "4500.00";
        item2Row.GetCell(4).HAlign = PdfCellHorizontalAlignment.Right;

        double subtotal = 7000.00;
        double cgst = subtotal * 0.09;
        double sgst = subtotal * 0.09;
        double total = subtotal + cgst + sgst;

        var totalsTable = page1.AddTable(350, 550, 212);
        totalsTable.SetColumnWidths(110, 102);

        totalsTable.GetOrCreateCell(0, 0).Text = "Subtotal";
        var subtotalValueCell = totalsTable.GetOrCreateCell(0, 1);
        subtotalValueCell.Text = subtotal.ToString("F2", CultureInfo.InvariantCulture);
        subtotalValueCell.HAlign = PdfCellHorizontalAlignment.Right;

        totalsTable.GetOrCreateCell(1, 0).Text = "CGST @ 9%";
        var cgstValueCell = totalsTable.GetOrCreateCell(1, 1);
        cgstValueCell.Text = cgst.ToString("F2", CultureInfo.InvariantCulture);
        cgstValueCell.HAlign = PdfCellHorizontalAlignment.Right;

        totalsTable.GetOrCreateCell(2, 0).Text = "SGST @ 9%";
        var sgstValueCell = totalsTable.GetOrCreateCell(2, 1);
        sgstValueCell.Text = sgst.ToString("F2", CultureInfo.InvariantCulture);
        sgstValueCell.HAlign = PdfCellHorizontalAlignment.Right;

        var totalCell = totalsTable.GetOrCreateCell(3, 0);
        totalCell.Text = "Total";
        totalCell.Background = PdfColor.LightGray;

        var totalValueCell = totalsTable.GetOrCreateCell(3, 1);
        totalValueCell.Text = total.ToString("F2", CultureInfo.InvariantCulture);
        totalValueCell.HAlign = PdfCellHorizontalAlignment.Right;
        totalValueCell.Background = PdfColor.LightGray;

        page1.AddText("Terms & Conditions:", 50, 450, 10);
        page1.AddText("1. Payment due within 30 days.", 50, 438, 9);
        page1.AddText("2. Goods once sold will not be taken back.", 50, 426, 9);

        var page2 = pdfDocument.AddPage();
        page2.AddText("This is the first line on the second page.", 50, 750, 12);

        var page3 = pdfDocument.AddPage();
        page3.AddText("This is the first line on the third page.", 50, 750, 12);

        return pdfDocument;
    }

    // Fractional geometry, colors, and every horizontal alignment, all with the default VAlign Middle.
    private static PdfDocument BuildMiddleTable()
    {
        var document = new PdfDocument(PdfPageSize.Letter);
        var page = document.AddPage();
        page.AddText("Middle-aligned table", 36, 740, 14);

        var table = page.AddTable(36.5, 700.25, 520.75);
        table.RowHeight = 24.5;
        table.SetColumnWidths(100.5, 150.25);
        table.AddColumn();

        var alignments = new[]
        {
            PdfCellHorizontalAlignment.Left,
            PdfCellHorizontalAlignment.Center,
            PdfCellHorizontalAlignment.Right
        };

        for (int r = 0; r < 4; r++)
        {
            var row = table.AddRow();
            for (int c = 0; c < 3; c++)
            {
                var cell = row.GetCell(c);
                cell.Text = $"R{r}C{c} (x)";
                cell.HAlign = alignments[(r + c) % 3];
                cell.Padding = 3.5;
                cell.Background = r == 0 ? new PdfColor(0.2, 0.4, 0.6) : PdfColor.White;
                cell.LeftBorder.Width = 0.75;
                cell.LeftBorder.Color = new PdfColor(0.1, 0.25, 0.125);
            }
        }

        table.SetText(5, 1, "Auto-expanded");
        return document;
    }

    // A 60-row table with a repeated header continuing across pages inside 36 pt margins, followed by a page the
    // caller added (WR-027, WR-028). Captured when table pagination was added; it locks that layout.
    private static PdfDocument BuildPaginatedTable()
    {
        var document = new PdfDocument { TopMargin = 36, BottomMargin = 36 };
        var page = document.AddPage();
        page.AddText("Paginated table", 50, 780, 14);

        var table = page.AddTable(50, 760, 495);
        table.SetColumnWidths(60, 285, 150);
        table.HeaderRowCount = 1;
        table.RepeatHeaderRows = true;

        var header = table.AddRow();
        header.GetCell(0).Text = "No.";
        header.GetCell(1).Text = "Item";
        header.GetCell(2).Text = "Amount";
        foreach (var cell in header.Cells) cell.Background = PdfColor.LightGray;

        for (int r = 1; r <= 60; r++)
        {
            var row = table.AddRow();
            row.GetCell(0).Text = r.ToString(CultureInfo.InvariantCulture);
            row.GetCell(1).Text = "Item " + r;
            row.GetCell(2).Text = (r * 12.5).ToString("0.00", CultureInfo.InvariantCulture);
            row.GetCell(2).HAlign = PdfCellHorizontalAlignment.Right;
        }

        var end = page.GetTableEnd(table);
        end.Page.AddText("End of table", 50, (int)(end.Y - 20), 10);
        foreach (var continuation in end.ContinuationPages)
        {
            continuation.AddText("Continued", 50, 20, 9);
        }

        document.AddPage().AddText("Next section", 50, 780, 14);
        return document;
    }

    // Plain text on several pages, including a page size change between pages.
    private static PdfDocument BuildTextPages()
    {
        var document = new PdfDocument();
        var first = document.AddPage();
        first.AddText("First page", 72, 760);
        first.AddText("Second line, size 9", 72, 740, 9);

        document.PageSize = PdfPageSize.A5;
        var second = document.AddPage();
        second.AddText("A5 page", 40, 560, 16);
        return document;
    }

    #endregion

    #endregion
}
