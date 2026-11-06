using UnlimitedPDF;

namespace UnlimitedPDF.Tests.Support;

/// <summary>
/// Builds the golden documents. Documents without images or the page layout API must stay byte-identical across those
/// features (AC-14 of the image story, AC-18 of the page layout story); the header-footer, flow-keep, and page-limit
/// documents pin the page layout API's own output (WR-030 to WR-033). Each builder uses fixed content.
/// </summary>
internal static class GoldenDocuments
{
    #region Fields and Constants

    public const string FlowKeep = "flow-keep";
    public const string HeaderFooter = "header-footer";
    public const string Invoice = "invoice";
    public const string MiddleTable = "middle-table";
    public const string PageLimit = "page-limit";
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
            FlowKeep => BuildFlowKeep(),
            HeaderFooter => BuildHeaderFooter(),
            Invoice => BuildInvoice(),
            MiddleTable => BuildMiddleTable(),
            PageLimit => BuildPageLimit(),
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

    // Body flow: a heading kept with a table that continues with its header row repeated, a block, a kept table, a
    // page break, and a caller page whose page-coordinate table shares its continuation page with the flow.
    private static PdfDocument BuildFlowKeep()
    {
        var document = new PdfDocument { MarginTop = 36, MarginBottom = 36, MarginLeft = 40, MarginRight = 40 };
        document.Footer.Height = 20;
        document.Footer.AddText("Page {page} of {pages}", 0, 4, 9, PdfTextAlignment.Right);

        var body = document.Body;
        body.AddText("Order lines", fontSize: 14, keepWithNext: true);
        var lines = body.AddTable(515, spacing: 6);
        lines.SetColumnWidths(60, 355, 100);
        lines.HeaderRowCount = 1;
        lines.RepeatHeaderRows = true;
        var header = lines.AddRow();
        header.GetCell(0).Text = "Line";
        header.GetCell(1).Text = "Item";
        header.GetCell(2).Text = "Amount";
        foreach (var cell in header.Cells) cell.BackgroundColor = PdfColor.LightGray;
        for (int r = 1; r <= 45; r++)
        {
            var row = lines.AddRow();
            row.GetCell(0).Text = r.ToString(CultureInfo.InvariantCulture);
            row.GetCell(1).Text = "Item " + r;
            row.GetCell(2).Text = (r * 7.25).ToString("0.00", CultureInfo.InvariantCulture);
            row.GetCell(2).HorizontalAlignment = PdfCellHorizontalAlignment.Right;
        }

        var totals = body.AddBlock(spacing: 12);
        totals.AddRectangle(515, 1, PdfColor.Black);
        totals.AddText("Total 7,498.75", spacing: 4, alignment: PdfTextAlignment.Right);
        totals.AddText("Thank you for your order", spacing: 2, fontSize: 9);

        body.PageBreak();
        var kept = body.AddTable(300, keepWithNext: true);
        kept.AddColumn();
        for (int r = 0; r < 3; r++) kept.AddRow().GetCell(0).Text = "Kept " + r;
        body.AddText("Follows the kept table", spacing: 8);

        var page = document.AddPage();
        var positioned = page.AddTable(40, 300, 250);
        positioned.AddColumn();
        for (int r = 0; r < 20; r++) positioned.AddRow().GetCell(0).Text = "Placed " + r;
        body.AddRectangle(200, 380, PdfColor.LightGray, x: 300);
        body.AddText("Continues below the carried rows", spacing: 10);
        return document;
    }

    // Header and footer bands: margins, clipped band content, a band table, page numbers, the per-page callback, and
    // First and Last rules, on caller pages and on a page-coordinate table's continuation page.
    private static PdfDocument BuildHeaderFooter()
    {
        var document = new PdfDocument { MarginTop = 24, MarginBottom = 24, MarginLeft = 40, MarginRight = 40 };
        document.Header.Height = 40;
        document.Header.AddText("Quarterly report", 0, 6, 14);
        document.Header.AddText("Confidential", 0, 8, 9, PdfTextAlignment.Right);
        document.Header.AddRectangle(0, 36, 515, 1, PdfColor.Black);
        document.Header.AddRectangle(480, 30, 40, 30, PdfColor.LightGray); // clipped at the band's bottom edge
        document.Footer.Height = 30;
        document.Footer.AddRectangle(0, 0, 515, 0.5, PdfColor.Black);
        document.Footer.AddText("Page {page} of {pages}", 0, 10, 9, PdfTextAlignment.Right);
        document.Footer.SetPageContent((content, info) => content.AddText(info.IsFirst ? "First page" : "Continued", 0, 10, 9));
        document.BodyMarginTop = 12;
        document.BodyMarginBottom = 12;

        var first = document.AddPageRule(PdfPageRuleTarget.First);
        first.Header.Height = 80;
        first.Header.AddText("Quarterly report", 0, 20, 24, PdfTextAlignment.Center);
        var codes = first.Header.AddTable(0, 56, 200);
        codes.SetColumnWidths(100, 100);
        var codeRow = codes.AddRow();
        codeRow.GetCell(0).Text = "Q3";
        codeRow.GetCell(1).Text = "2026";
        document.AddPageRule(PdfPageRuleTarget.Last).Footer.AddText("End of report", 0, 10, 9, PdfTextAlignment.Center);

        document.AddPage().AddText("Summary", 40, 640, 12);
        var detail = document.AddPage();
        var table = detail.AddTable(40, 700, 515);
        table.SetColumnWidths(100, 415);
        for (int r = 0; r < 40; r++)
        {
            var row = table.AddRow();
            row.GetCell(0).Text = r.ToString(CultureInfo.InvariantCulture);
            row.GetCell(1).Text = "Detail " + r;
        }
        return document;
    }

    // A strict page limit met exactly: the flow fills two pages and Body.MaxPages is 2.
    private static PdfDocument BuildPageLimit()
    {
        var document = new PdfDocument { MarginTop = 36, MarginBottom = 36, MarginLeft = 40, MarginRight = 40 };
        document.Body.MaxPages = 2;
        for (int i = 1; i <= 100; i++)
            document.Body.AddText("Line " + i.ToString(CultureInfo.InvariantCulture), fontSize: 10);
        return document;
    }

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
        headerRow.GetCell(0).BackgroundColor = PdfColor.LightGray;
        headerRow.GetCell(1).Text = "Item Description";
        headerRow.GetCell(1).BackgroundColor = PdfColor.LightGray;
        headerRow.GetCell(2).Text = "Qty";
        headerRow.GetCell(2).BackgroundColor = PdfColor.LightGray;
        headerRow.GetCell(3).Text = "Rate (INR)";
        headerRow.GetCell(3).BackgroundColor = PdfColor.LightGray;
        headerRow.GetCell(4).Text = "Amount (INR)";
        headerRow.GetCell(4).BackgroundColor = PdfColor.LightGray;

        var item1Row = itemsTable.AddRow();
        item1Row.GetCell(0).Text = "1";
        item1Row.GetCell(1).Text = "Wireless Keyboard";
        item1Row.GetCell(2).Text = "2";
        item1Row.GetCell(2).HorizontalAlignment = PdfCellHorizontalAlignment.Right;
        item1Row.GetCell(3).Text = "1250.00";
        item1Row.GetCell(3).HorizontalAlignment = PdfCellHorizontalAlignment.Right;
        item1Row.GetCell(4).Text = "2500.00";
        item1Row.GetCell(4).HorizontalAlignment = PdfCellHorizontalAlignment.Right;

        var item2Row = itemsTable.AddRow();
        item2Row.GetCell(0).Text = "2";
        item2Row.GetCell(1).Text = "USB-C Docking Station";
        item2Row.GetCell(2).Text = "1";
        item2Row.GetCell(2).HorizontalAlignment = PdfCellHorizontalAlignment.Right;
        item2Row.GetCell(3).Text = "4500.00";
        item2Row.GetCell(3).HorizontalAlignment = PdfCellHorizontalAlignment.Right;
        item2Row.GetCell(4).Text = "4500.00";
        item2Row.GetCell(4).HorizontalAlignment = PdfCellHorizontalAlignment.Right;

        double subtotal = 7000.00;
        double cgst = subtotal * 0.09;
        double sgst = subtotal * 0.09;
        double total = subtotal + cgst + sgst;

        var totalsTable = page1.AddTable(350, 550, 212);
        totalsTable.SetColumnWidths(110, 102);

        totalsTable.GetOrCreateCell(0, 0).Text = "Subtotal";
        var subtotalValueCell = totalsTable.GetOrCreateCell(0, 1);
        subtotalValueCell.Text = subtotal.ToString("F2", CultureInfo.InvariantCulture);
        subtotalValueCell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;

        totalsTable.GetOrCreateCell(1, 0).Text = "CGST @ 9%";
        var cgstValueCell = totalsTable.GetOrCreateCell(1, 1);
        cgstValueCell.Text = cgst.ToString("F2", CultureInfo.InvariantCulture);
        cgstValueCell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;

        totalsTable.GetOrCreateCell(2, 0).Text = "SGST @ 9%";
        var sgstValueCell = totalsTable.GetOrCreateCell(2, 1);
        sgstValueCell.Text = sgst.ToString("F2", CultureInfo.InvariantCulture);
        sgstValueCell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;

        var totalCell = totalsTable.GetOrCreateCell(3, 0);
        totalCell.Text = "Total";
        totalCell.BackgroundColor = PdfColor.LightGray;

        var totalValueCell = totalsTable.GetOrCreateCell(3, 1);
        totalValueCell.Text = total.ToString("F2", CultureInfo.InvariantCulture);
        totalValueCell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;
        totalValueCell.BackgroundColor = PdfColor.LightGray;

        page1.AddText("Terms & Conditions:", 50, 450, 10);
        page1.AddText("1. Payment due within 30 days.", 50, 438, 9);
        page1.AddText("2. Goods once sold will not be taken back.", 50, 426, 9);

        var page2 = pdfDocument.AddPage();
        page2.AddText("This is the first line on the second page.", 50, 750, 12);

        var page3 = pdfDocument.AddPage();
        page3.AddText("This is the first line on the third page.", 50, 750, 12);

        return pdfDocument;
    }

    // Fractional geometry, colors, and every horizontal alignment, all with the default VerticalAlignment Middle.
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
                cell.HorizontalAlignment = alignments[(r + c) % 3];
                cell.Padding = 3.5;
                cell.BackgroundColor = r == 0 ? new PdfColor(0.2, 0.4, 0.6) : PdfColor.White;
                cell.BorderLeft.Width = 0.75;
                cell.BorderLeft.Color = new PdfColor(0.1, 0.25, 0.125);
            }
        }

        table.SetText(5, 1, "Auto-expanded");
        return document;
    }

    // A 60-row table with a repeated header continuing across pages inside 36 pt margins, followed by a page the
    // caller added (WR-027, WR-028). Captured when table pagination was added; it locks that layout.
    private static PdfDocument BuildPaginatedTable()
    {
        var document = new PdfDocument { MarginTop = 36, MarginBottom = 36 };
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
        foreach (var cell in header.Cells) cell.BackgroundColor = PdfColor.LightGray;

        for (int r = 1; r <= 60; r++)
        {
            var row = table.AddRow();
            row.GetCell(0).Text = r.ToString(CultureInfo.InvariantCulture);
            row.GetCell(1).Text = "Item " + r;
            row.GetCell(2).Text = (r * 12.5).ToString("0.00", CultureInfo.InvariantCulture);
            row.GetCell(2).HorizontalAlignment = PdfCellHorizontalAlignment.Right;
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
