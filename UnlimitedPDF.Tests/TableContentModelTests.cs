using System.ComponentModel;
using System.Reflection;
using UnlimitedPDF;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The table cell content model and row-height settings, without rendering: ordered content, the <c>Text</c> shortcut,
/// content methods, emptiness, structure changes, and validation (AC-12 model part, AC-13, AC-28, AC-29; D-022,
/// D-023, D-032, D-043, D-044; FA-06).
/// </summary>
public class TableContentModelTests
{
    #region Fields and Constants

    private static readonly PdfImage Logo = PdfImage.FromBytes(new JpegBuilder { Width = 200, Height = 80 }.Build());
    private static readonly PdfImage Photo = PdfImage.FromBytes(new JpegBuilder { Width = 64, Height = 48 }.Build());

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Clone_copies_content_and_line_height()
    {
        var table = new PdfTable();
        var cell = table.GetOrCreateCell(0, 0);
        cell.SetContent(Photo, "Laptop");
        cell.LineHeightFactor = 1.5;

        var clone = table.Rows[0].CloneCellAt(0);

        Assert.Equal(Describe(cell), Describe(clone));
        Assert.Equal(1.5, clone.LineHeightFactor);
        clone.ClearContent();
        Assert.Equal(2, cell.Content.Count);
    }

    [Fact]
    public void Content_is_a_read_only_view()
    {
        var property = typeof(PdfCell).GetProperty(nameof(PdfCell.Content))!;

        Assert.False(property.CanWrite);
        Assert.Equal(typeof(IReadOnlyList<PdfCellContent>), property.PropertyType);
    }

    [Fact]
    public void Content_items_reject_null_values()
    {
        Assert.Equal("text", Assert.Throws<ArgumentNullException>(() => new PdfCellText(null!)).ParamName);
        Assert.Equal("image", Assert.Throws<ArgumentNullException>(() => new PdfCellImage(null!)).ParamName);
    }

    [Fact]
    public void Content_methods_add_insert_remove_and_clear()
    {
        var cell = new PdfCell();

        cell.AddContent("SKU 123");
        cell.InsertContent(0, Photo);
        cell.InsertContent(1, "Laptop");
        cell.AddContent(Logo);
        Assert.Equal(new[] { "image:64", "Laptop", "SKU 123", "image:200" }, Describe(cell));

        cell.RemoveContent(3);
        Assert.Equal(new[] { "image:64", "Laptop", "SKU 123" }, Describe(cell));

        cell.ClearContent();
        Assert.Empty(cell.Content);
        Assert.Equal("", cell.Text);
    }

    [Fact]
    public void Content_methods_reject_null_items_and_bad_indexes()
    {
        var cell = new PdfCell();
        cell.AddContent("one");

        Assert.Equal("item", Assert.Throws<ArgumentNullException>(() => cell.AddContent(null!)).ParamName);
        Assert.Equal("item", Assert.Throws<ArgumentNullException>(() => cell.AddContent((string)null!)).ParamName);
        Assert.Equal("item", Assert.Throws<ArgumentNullException>(() => cell.InsertContent(0, null!)).ParamName);
        Assert.Equal("index", Assert.Throws<ArgumentOutOfRangeException>(() => cell.InsertContent(2, "x")).ParamName);
        Assert.Equal("index", Assert.Throws<ArgumentOutOfRangeException>(() => cell.InsertContent(-1, "x")).ParamName);
        Assert.Equal("index", Assert.Throws<ArgumentOutOfRangeException>(() => cell.RemoveContent(1)).ParamName);
        Assert.Equal(new[] { "one" }, Describe(cell));
    }

    [Fact]
    public void Edited_table_methods_carry_their_first_version()
    {
        foreach (var name in new[] { "EnsureSize", "GetCell", "GetOrCreateCell", "SetCell" })
        {
            var method = typeof(PdfTable).GetMethod(name)!;
            Assert.Equal("0.0.3", method.GetCustomAttribute<AddedVersionAttribute>()?.Version);
            Assert.False(string.IsNullOrWhiteSpace(method.GetCustomAttribute<DescriptionAttribute>()?.Description));
        }

        // Renamed in 1.0.0, so the name first shipped then.
        var trim = typeof(PdfTable).GetMethod(nameof(PdfTable.TrimEmptyTrailingRowsAndColumns))!;
        Assert.Equal("1.0.0", trim.GetCustomAttribute<AddedVersionAttribute>()?.Version);
    }

    [Fact]
    public void Implicit_conversions_create_items()
    {
        PdfCellContent text = "Laptop";
        PdfCellContent image = Photo;

        Assert.Equal("Laptop", Assert.IsType<PdfCellText>(text).Text);
        Assert.Same(Photo, Assert.IsType<PdfCellImage>(image).Image);
    }

    [Fact]
    public void Line_height_factor_defaults_to_1_2_and_rejects_invalid_values()
    {
        var cell = new PdfCell();

        Assert.Equal(1.2, cell.LineHeightFactor);
        cell.LineHeightFactor = 1.0;
        Assert.Equal(1.0, cell.LineHeightFactor);
        foreach (var value in new[] { 0, -1, double.NaN, double.PositiveInfinity })
            Assert.Equal("value", Assert.Throws<ArgumentOutOfRangeException>(() => cell.LineHeightFactor = value).ParamName);
    }

    [Fact]
    public void New_public_members_carry_version_metadata()
    {
        var types = new[] { typeof(PdfCellContent), typeof(PdfCellText), typeof(PdfCellImage) };
        var members = types.SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName || m.Name == "op_Implicit").Cast<MethodBase>()
                .Concat(t.GetConstructors()))
            .Concat(typeof(PdfCell).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName));

        Assert.All(members, member =>
        {
            Assert.Equal("1.0.0", member.GetCustomAttribute<AddedVersionAttribute>()?.Version);
            Assert.False(string.IsNullOrWhiteSpace(member.GetCustomAttribute<DescriptionAttribute>()?.Description));
        });
    }

    [Fact]
    public void Row_and_table_height_settings_default_and_validate()
    {
        var table = new PdfTable();
        var row = table.AddRow();

        Assert.Equal(PdfRowHeightMode.Exactly, table.RowHeightMode);
        Assert.Null(row.Height);
        Assert.Null(row.HeightMode);

        row.Height = 44.8;
        row.HeightMode = PdfRowHeightMode.AtLeast;
        Assert.Equal(44.8, row.Height);
        Assert.Equal(PdfRowHeightMode.AtLeast, row.HeightMode);
        row.Height = null;
        Assert.Null(row.Height);

        foreach (var value in new[] { 0, -5, double.NaN, double.NegativeInfinity })
            Assert.Equal("value", Assert.Throws<ArgumentOutOfRangeException>(() => row.Height = value).ParamName);
    }

    [Fact]
    public void Set_content_replaces_everything_from_params_or_a_list()
    {
        // AC-29: replacing photo, "Laptop", "SKU 123" with logo, "Tablet" in one call.
        var cell = new PdfCell();
        cell.SetContent(Photo, "Laptop", "SKU 123");

        cell.SetContent(Logo, "Tablet");
        Assert.Equal(new[] { "image:200", "Tablet" }, Describe(cell));

        cell.SetContent(new List<PdfCellContent> { "a", Photo });
        Assert.Equal(new[] { "a", "image:64" }, Describe(cell));

        cell.SetContent();
        Assert.Empty(cell.Content);
    }

    [Fact]
    public void Set_content_with_a_null_item_changes_nothing()
    {
        var cell = new PdfCell();
        cell.SetContent(Photo, "Laptop");

        var exception = Assert.Throws<ArgumentException>(() => cell.SetContent(Logo, null!, "x"));
        Assert.Equal("items", exception.ParamName);
        Assert.Contains("index 1", exception.Message);
        Assert.Equal("items", Assert.Throws<ArgumentNullException>(() => cell.SetContent((PdfCellContent[])null!)).ParamName);
        Assert.Equal("items", Assert.Throws<ArgumentNullException>(() => cell.SetContent((IEnumerable<PdfCellContent>)null!)).ParamName);

        Assert.Equal(new[] { "image:64", "Laptop" }, Describe(cell));
    }

    [Fact]
    public void Structure_changes_keep_content_with_its_cell()
    {
        var table = new PdfTable();
        table.GetOrCreateCell(1, 1).SetContent(Photo, "Laptop");

        table.InsertRow(0);
        table.InsertColumn(0);
        Assert.Equal(new[] { "image:64", "Laptop" }, Describe(table.GetCell(2, 2)));

        table.RemoveRow(0);
        table.RemoveColumn(0);
        Assert.Equal(new[] { "image:64", "Laptop" }, Describe(table.GetCell(1, 1)));

        var placed = new PdfCell();
        placed.SetContent(Logo);
        table.SetCell(3, 0, placed);
        Assert.Same(placed, table.GetCell(3, 0));
        Assert.Equal(new[] { "image:200" }, Describe(table.GetCell(3, 0)));
    }

    [Fact]
    public void Table_guards_name_the_invalid_argument()
    {
        var table = new PdfTable();
        table.EnsureSize(1, 1);

        Assert.Equal("rows", Assert.Throws<ArgumentOutOfRangeException>(() => table.EnsureSize(-1, 0)).ParamName);
        Assert.Equal("columns", Assert.Throws<ArgumentOutOfRangeException>(() => table.EnsureSize(0, -1)).ParamName);
        Assert.Equal("row", Assert.Throws<ArgumentOutOfRangeException>(() => table.GetCell(-1, 0)).ParamName);
        Assert.Equal("row", Assert.Throws<ArgumentOutOfRangeException>(() => table.GetCell(1, 0)).ParamName);
        Assert.Equal("column", Assert.Throws<ArgumentOutOfRangeException>(() => table.GetCell(0, 1)).ParamName);
        Assert.Equal("column", Assert.Throws<ArgumentOutOfRangeException>(() => table.GetOrCreateCell(0, -1)).ParamName);
        Assert.Equal("row", Assert.Throws<ArgumentOutOfRangeException>(() => table.SetCell(-1, 0, new PdfCell())).ParamName);
        Assert.Equal("column", Assert.Throws<ArgumentOutOfRangeException>(() => table.SetCell(0, -1, new PdfCell())).ParamName);
        Assert.Equal("columnIndex", Assert.Throws<ArgumentOutOfRangeException>(() => table.Rows[0].GetCell(1)).ParamName);
        Assert.Equal("columnIndex", Assert.Throws<ArgumentOutOfRangeException>(() => table.Rows[0].SetCell(-1, new PdfCell())).ParamName);
    }

    [Fact]
    public void Text_on_cells_without_text_reads_empty_and_appends_on_write()
    {
        var cell = new PdfCell();
        Assert.Equal("", cell.Text);

        cell.AddContent(Photo);
        Assert.Equal("", cell.Text);

        cell.Text = "Caption";
        Assert.Equal(new[] { "image:64", "Caption" }, Describe(cell));

        cell.Text = null!;
        Assert.Equal("", cell.Text);
        Assert.Equal(new[] { "image:64", "" }, Describe(cell));
    }

    [Fact]
    public void Text_only_cells_and_set_text_behave_as_before()
    {
        var table = new PdfTable();
        table.SetText(0, 0, "Total");
        table.SetText(0, 0, "Grand total");
        table.SetText(0, 1, null!);

        Assert.Equal("Grand total", table.GetCell(0, 0).Text);
        Assert.Equal("", table.GetCell(0, 1).Text);

        var withImage = table.GetOrCreateCell(1, 0);
        withImage.AddContent(Photo);
        table.SetText(1, 0, "Photo caption");
        Assert.Equal(new[] { "image:64", "Photo caption" }, Describe(withImage));
    }

    [Fact]
    public void Text_reads_and_writes_only_the_first_text_item()
    {
        // AC-28: content photo, "Laptop", "SKU 123"; reading gives "Laptop"; setting "Tablet" keeps the others.
        var cell = new PdfCell();
        cell.SetContent(Photo, "Laptop", "SKU 123");

        Assert.Equal("Laptop", cell.Text);

        cell.Text = "Tablet";
        Assert.Equal(new[] { "image:64", "Tablet", "SKU 123" }, Describe(cell));
    }

    [Fact]
    public void Trim_keeps_image_cells_and_removes_empty_text_cells()
    {
        var table = new PdfTable();
        table.SetText(0, 0, "Header");
        table.GetOrCreateCell(1, 2).AddContent(Logo);   // Only an image in the last column of row 1.
        table.SetText(2, 0, "");                         // Empty text item only: still empty.
        table.SetText(2, 3, "");

        table.TrimEmptyTrailingRowsAndColumns();

        Assert.Equal(2, table.RowCount);
        Assert.Equal(3, table.ColumnCount);
        Assert.Equal(new[] { "image:200" }, Describe(table.GetCell(1, 2)));
    }

    #endregion

    #region Private Methods

    private static string[] Describe(PdfCell cell) => cell.Content
        .Select(item => item switch
        {
            PdfCellText text => text.Text,
            PdfCellImage image => $"image:{image.Image.PixelWidth}",
            _ => "?"
        })
        .ToArray();

    #endregion

    #endregion
}
