using System.Text.RegularExpressions;
using UnlimitedPDF;
using UnlimitedPDF.Models.Internal;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Table rendering with images: height rules, content stacking, vertical alignment, the AC-20 layout error, escaped
/// cell text, and invariant numbers (AC-10 to AC-12, AC-14, AC-15, AC-20, AC-22; D-014, D-022, D-024, D-031, D-032,
/// D-047; FA-04, FA-05, FA-08; A05, A06, A10).
/// </summary>
public class TableRenderingTests
{
    #region Fields and Constants

    private const int Precision = 9;

    // A 200 x 80 px logo: 150 x 60 pt at its natural size.
    private static readonly PdfImage Logo = PdfImage.FromBytes(new JpegBuilder { Width = 200, Height = 80 }.Build());

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void At_least_row_grows_to_fit_an_image_and_rows_below_move_down()
    {
        // AC-10: in a 100 pt column with 4 pt padding the logo is 92 x 36.8 and the AtLeast row grows to 44.8 pt.
        var table = SingleCellTable(cell => cell.AddContent(Logo), PdfRowHeightMode.AtLeast);
        table.GetOrCreateCell(1, 0).Text = "next";

        var layout = PdfTableRenderer.Layout(table, 1, 1);
        Assert.Equal(92, layout.Cells[0, 0].Items[0].Width, Precision);
        Assert.Equal(36.8, layout.Cells[0, 0].Items[0].Height, Precision);
        Assert.Equal(44.8, layout.RowHeights[0], Precision);

        // The second row starts below the grown first row: its rectangle's bottom is 700 - 44.8 - 22.4. Table
        // geometry keeps the original number format, so the value is compared numerically.
        var rectangles = Regex.Matches(Render(table), @"^50 (\S+) 100 (\S+) re f", RegexOptions.Multiline);
        Assert.Equal(2, rectangles.Count);
        Assert.Equal(655.2, double.Parse(rectangles[0].Groups[1].Value, CultureInfo.InvariantCulture), Precision);
        Assert.Equal(632.8, double.Parse(rectangles[1].Groups[1].Value, CultureInfo.InvariantCulture), Precision);
        Assert.Equal(22.4, double.Parse(rectangles[1].Groups[2].Value, CultureInfo.InvariantCulture), Precision);
    }

    [Fact]
    public void At_least_text_row_uses_the_line_height_factor()
    {
        // AC-22: a 12 pt line takes 1.2 x 12 = 14.4 pt by default and 12 pt with a factor of 1.0.
        var table = SingleCellTable(cell => cell.Text = "Text", PdfRowHeightMode.AtLeast, rowHeight: 10);
        Assert.Equal(14.4 + 8, PdfTableRenderer.Layout(table, 1, 1).RowHeights[0], Precision);

        table.GetCell(0, 0).LineHeightFactor = 1.0;
        Assert.Equal(20, PdfTableRenderer.Layout(table, 1, 1).RowHeights[0], Precision);
    }

    [Theory]
    [InlineData("Rate (INR)", "(Rate (INR)) Tj")]
    [InlineData(@"C:\temp\new", @"(C:\\temp\\new) Tj")]
    [InlineData("Smiley :)", @"(Smiley :\)) Tj")]
    [InlineData("x) Tj 1 0 0 rg 0 0 600 850 re f (", @"(x\) Tj 1 0 0 rg 0 0 600 850 re f \() Tj")]
    public void Cell_text_is_escaped_minimally(string text, string expected)
    {
        // D-047: backslashes always, parentheses only when unbalanced; balanced text is unchanged.
        var table = SingleCellTable(cell => cell.Text = text);

        Assert.Contains(expected, Render(table));
    }

    [Fact]
    public void Default_tables_keep_exactly_rows()
    {
        var table = SingleCellTable(cell => cell.AddContent(Logo));

        Assert.Equal(20, PdfTableRenderer.Layout(table, 1, 1).RowHeights[0]);
    }

    [Fact]
    public void Empty_text_items_take_no_space_and_draw_nothing()
    {
        var table = SingleCellTable(cell => cell.SetContent("", Logo, ""), PdfRowHeightMode.AtLeast);

        var layout = PdfTableRenderer.Layout(table, 1, 1);
        Assert.Single(layout.Cells[0, 0].Items);
        Assert.DoesNotContain("() Tj", Render(table));
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("(a) and (b)", "(a) and (b)")]
    [InlineData("((a)", @"\((a)")]
    [InlineData("(a))", @"(a)\)")]
    [InlineData(")(", @"\)\(")]
    [InlineData(@"a\b", @"a\\b")]
    [InlineData(@"\(", @"\\\(")]
    public void Escaper_handles_nesting_and_order(string text, string expected)
    {
        Assert.Equal(expected, PdfLiteralString.Escape(text));
    }

    [Fact]
    public void Exactly_row_fits_an_image_into_the_padded_cell()
    {
        // AC-10: the logo in an Exactly 20 pt row with 4 pt padding renders 30 x 12 pt, centered in the 100 pt column.
        var table = SingleCellTable(cell =>
        {
            cell.AddContent(Logo);
            cell.HorizontalAlignment = PdfCellHorizontalAlignment.Center;
        });

        var layout = PdfTableRenderer.Layout(table, 1, 1);
        Assert.Equal(30, layout.Cells[0, 0].Items[0].Width, Precision);
        Assert.Equal(12, layout.Cells[0, 0].Items[0].Height, Precision);

        // Centered horizontally (x = 50 + 35) and vertically (bottom = 700 - 4 - 12).
        Assert.Contains("q 30 0 0 12 85 684 cm /Im1 Do Q", Render(table));
    }

    [Fact]
    public void Exactly_row_shares_remaining_height_among_images()
    {
        // Two logos (36.8 pt each at the padded width) share 40 - 8 - 14.4 = 17.6 pt by one factor.
        var table = SingleCellTable(cell => cell.SetContent(Logo, "caption", Logo), rowHeight: 40);

        var items = PdfTableRenderer.Layout(table, 1, 1).Cells[0, 0].Items;
        Assert.Equal(8.8, items[0].Height, Precision);
        Assert.Equal(8.8, items[2].Height, Precision);
        Assert.Equal(22, items[0].Width, Precision);
    }

    [Fact]
    public void Exactly_row_without_room_fails_save_before_writing()
    {
        // AC-20: an Exactly 20 pt row holding a logo and 12 pt text (a 14.4 pt line) leaves the image no height.
        var document = new PdfDocument();
        document.AddPage().AddText("first page", 10, 10);
        var page = document.AddPage();
        page.AddTable(10, 800, 200).GetOrCreateCell(0, 0).Text = "first table";
        var table = page.AddTable(50, 700, 100);
        table.SetColumnWidths(100);
        table.GetOrCreateCell(0, 0).Text = "header";
        table.GetOrCreateCell(1, 0).SetContent(Logo, "Logo");

        using var stream = new MemoryStream();
        var exception = Assert.Throws<InvalidOperationException>(() => document.Save(stream));

        Assert.Equal(0, stream.Length);
        Assert.Contains("table 2 on page 2, row index 1, column index 0", exception.Message);
        Assert.Contains("an Exactly row of 20 pt", exception.Message);
        Assert.Contains("PdfRowHeightMode.AtLeast", exception.Message);
    }

    [Fact]
    public void Exactly_row_without_room_leaves_an_existing_file_unchanged()
    {
        var document = new PdfDocument();
        var table = document.AddPage().AddTable(50, 700, 100);
        table.SetColumnWidths(100);
        table.GetOrCreateCell(0, 0).SetContent(Logo, "Logo");
        var path = Path.Combine(Path.GetTempPath(), $"unlimitedpdf-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
        try
        {
            Assert.Throws<InvalidOperationException>(() => document.Save(path));
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Image_without_width_after_padding_fails()
    {
        var table = SingleCellTable(cell =>
        {
            cell.AddContent(Logo);
            cell.Padding = 50;
        }, PdfRowHeightMode.AtLeast);

        var exception = Assert.Throws<InvalidOperationException>(() => PdfTableRenderer.Layout(table, 1, 1));
        Assert.Contains("no width left", exception.Message);
    }

    [Fact]
    public void Images_align_horizontally_like_text()
    {
        var right = SingleCellTable(cell =>
        {
            cell.AddContent(PdfImage.FromBytes(new JpegBuilder { Width = 40, Height = 20 }.Build()));
            cell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;
        }, PdfRowHeightMode.AtLeast);

        // 40 x 20 px = 30 x 15 pt; right aligned: 50 + 100 - 30 - 4 = 116.
        Assert.Contains("q 30 0 0 15 116 681 cm /Im1 Do Q", Render(right));
    }

    [Fact]
    public void Images_in_tables_share_resources_with_page_images()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.AddImage(Logo, 400, 760);
        var table = page.AddTable(50, 700, 100);
        table.RowHeightMode = PdfRowHeightMode.AtLeast;
        table.GetOrCreateCell(0, 0).AddContent(Logo);
        table.GetOrCreateCell(1, 0).AddContent(PdfImage.FromBytes(new PngBuilder { ColorType = 6 }.Build()));

        using var stream = new MemoryStream();
        document.Save(stream);
        var pdf = stream.ToArray();
        var text = Encoding.Latin1.GetString(pdf);

        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal(3, Regex.Matches(text, @"/Subtype /Image\b").Count); // Logo, PNG, and the PNG's mask.
        Assert.Equal(2, Regex.Matches(text, @"/Im1 Do").Count);
        Assert.Single(Regex.Matches(text, @"/Im2 Do"));
    }

    [Fact]
    public void Larger_text_uses_its_own_line_height()
    {
        // FA-04: the formula scales with font size (24 pt text takes 28.8 pt); public cells use 12 pt.
        var table = SingleCellTable(cell => cell.AddContent(new PdfCellText("Big", 24)), PdfRowHeightMode.AtLeast, rowHeight: 10);

        Assert.Equal(28.8, PdfTableRenderer.Layout(table, 1, 1).Cells[0, 0].BlockHeight, Precision);
    }

    [Fact]
    public void Numbers_are_invariant_under_comma_decimal_cultures()
    {
        // AC-15 and FA-08: table geometry, colors, and image placements use '.' in every culture.
        using var culture = new CultureScope("de-DE");
        var document = new PdfDocument();
        var table = document.AddPage().AddTable(36.5, 700.25, 220.75);
        table.RowHeightMode = PdfRowHeightMode.AtLeast;
        table.SetColumnWidths(100.5, 120.25);
        var cell = table.GetOrCreateCell(0, 0);
        cell.SetContent(Logo, "Logo");
        cell.BackgroundColor = new PdfColor(0.2, 0.4, 0.6);
        table.GetOrCreateCell(0, 1).Text = "Value";

        using var stream = new MemoryStream();
        document.Save(stream);
        var text = Encoding.Latin1.GetString(stream.ToArray());

        PdfStructureChecker.AssertValid(stream.ToArray());
        Assert.DoesNotMatch(@"\d,\d", text);
        Assert.Contains("0.200 0.400 0.600 rg", text);
    }

    [Fact]
    public void Stacked_content_draws_top_to_bottom_in_order()
    {
        // AC-11: photo, "Laptop", "SKU 123" in an AtLeast row are drawn in that order, each below the previous.
        var table = SingleCellTable(cell => cell.SetContent(Logo, "Laptop", "SKU 123"), PdfRowHeightMode.AtLeast);

        var content = Render(table);
        int image = content.IndexOf(" Do Q", StringComparison.Ordinal);
        int laptop = content.IndexOf("(Laptop) Tj", StringComparison.Ordinal);
        int sku = content.IndexOf("(SKU 123) Tj", StringComparison.Ordinal);
        Assert.True(image < laptop && laptop < sku, content);

        // Row: 36.8 + 14.4 + 14.4 + 8 = 73.6. Image bottom = 700 - 4 - 36.8; "Laptop" baseline = 659.2 - 7.2 - 4.
        Assert.Equal(73.6, PdfTableRenderer.Layout(table, 1, 1).RowHeights[0], Precision);
        Assert.Contains("q 92 0 0 36.8 54 659.2 cm /Im1 Do Q", content);
        Assert.Contains("1 0 0 1 54 648 Tm", content);
        Assert.Contains("1 0 0 1 54 633.6 Tm", content);
    }

    [Fact]
    public void Stacked_image_and_text_follow_story_example()
    {
        // AC-22: a logo scaled to 36.8 pt above 12 pt text in an AtLeast row with 4 pt padding makes the row 59.2 pt.
        var table = SingleCellTable(cell => cell.SetContent(Logo, "Logo"), PdfRowHeightMode.AtLeast);

        Assert.Equal(59.2, PdfTableRenderer.Layout(table, 1, 1).RowHeights[0], Precision);
    }

    [Fact]
    public void Vertical_alignment_positions_images()
    {
        var top = SingleCellTable(cell =>
        {
            cell.AddContent(PdfImage.FromBytes(new JpegBuilder { Width = 40, Height = 8 }.Build())); // 30 x 6 pt
            cell.VerticalAlignment = PdfCellVerticalAlignment.Top;
        }, rowHeight: 30);

        // Top: bottom edge at 700 - 4 - 6.
        Assert.Contains("q 30 0 0 6 54 690 cm /Im1 Do Q", Render(top));
    }

    [Theory]
    [InlineData(PdfCellVerticalAlignment.Top, "1 0 0 1 54 684.8 Tm")]
    [InlineData(PdfCellVerticalAlignment.Bottom, "1 0 0 1 54 687.2 Tm")]
    [InlineData(PdfCellVerticalAlignment.Middle, "1 0 0 1 54 686 Tm")]
    public void Vertical_alignment_positions_the_text_block(PdfCellVerticalAlignment alignment, string expected)
    {
        // Row 700..680, padding 4, line 14.4 pt. Top: 700 - 4 - 7.2 - 4; Bottom: 680 + 4 + 14.4 - 7.2 - 4;
        // Middle keeps the original 700 - 10 - 4.
        var table = SingleCellTable(cell =>
        {
            cell.Text = "Text";
            cell.VerticalAlignment = alignment;
        });

        Assert.Contains(expected, Render(table));
    }

    #endregion

    #region Private Methods

    private static string Render(PdfTable table)
    {
        int count = 0;
        var names = new Dictionary<string, string>();
        return PdfTableRenderer.Render(table, 1, 1, new PageDrawSink(image =>
        {
            if (!names.TryGetValue(image.ContentKey, out var name))
                names[image.ContentKey] = name = $"/Im{++count}";
            return name;
        }));
    }

    private static PdfTable SingleCellTable(Action<PdfCell> fill, PdfRowHeightMode rule = PdfRowHeightMode.Exactly, double rowHeight = 20)
    {
        var table = new PdfTable { X = 50, Y = 700, Width = 100, RowHeight = rowHeight, RowHeightMode = rule };
        table.SetColumnWidths(100);
        fill(table.GetOrCreateCell(0, 0));
        return table;
    }

    #endregion

    #endregion
}
