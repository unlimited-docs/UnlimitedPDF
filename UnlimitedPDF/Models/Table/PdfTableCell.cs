namespace UnlimitedPDF.Models.Table;

/// <summary>
/// A table cell. Its content is an ordered list of text and image items, drawn top to bottom in the order they were
/// added. <see cref="Text"/> is a shortcut for the first text item, so text-only cells work as before.
/// </summary>
public class PdfTableCell
{
    #region Fields and Constants

    private readonly List<PdfCellContent> _content = new();
    private double _lineHeightFactor = 1.2;

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Adds an item to the end of the cell's content.
    /// </summary>
    /// <param name="item">The item. A string or a <see cref="PdfImage"/> converts to an item implicitly.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    [AddedVersion("0.1.0")]
    [Description("Adds an item to the end of the cell's content.")]
    public void AddContent(PdfCellContent item)
    {
        if (item is null) throw new ArgumentNullException(nameof(item));
        _content.Add(item);
    }

    /// <summary>
    /// Removes every item from the cell's content.
    /// </summary>
    [AddedVersion("0.1.0")]
    [Description("Removes every item from the cell's content.")]
    public void ClearContent()
    {
        _content.Clear();
    }

    /// <summary>
    /// Inserts an item into the cell's content at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The position, from 0 to the number of items.</param>
    /// <param name="item">The item. A string or a <see cref="PdfImage"/> converts to an item implicitly.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the content.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    [AddedVersion("0.1.0")]
    [Description("Inserts an item into the cell's content at the given position.")]
    public void InsertContent(int index, PdfCellContent item)
    {
        if (index < 0 || index > _content.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (item is null) throw new ArgumentNullException(nameof(item));
        _content.Insert(index, item);
    }

    /// <summary>
    /// Removes the item at <paramref name="index"/> from the cell's content.
    /// </summary>
    /// <param name="index">The position of the item.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the content.</exception>
    [AddedVersion("0.1.0")]
    [Description("Removes the item at the given position from the cell's content.")]
    public void RemoveContent(int index)
    {
        if (index < 0 || index >= _content.Count) throw new ArgumentOutOfRangeException(nameof(index));
        _content.RemoveAt(index);
    }

    /// <summary>
    /// Replaces the cell's whole content with <paramref name="items"/>, in order. Nothing changes if any item is null.
    /// </summary>
    /// <param name="items">The new items.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="items"/> contains a null item.</exception>
    [AddedVersion("0.1.0")]
    [Description("Replaces the cell's whole content with the given items, in order.")]
    public void SetContent(IEnumerable<PdfCellContent> items)
    {
        if (items is null) throw new ArgumentNullException(nameof(items));

        var replacement = items.ToList();
        int nullIndex = replacement.FindIndex(item => item is null);
        if (nullIndex >= 0) throw new ArgumentException($"The item at index {nullIndex} is null.", nameof(items));

        _content.Clear();
        _content.AddRange(replacement);
    }

    /// <summary>
    /// Replaces the cell's whole content with <paramref name="items"/>, in order, for example
    /// <c>cell.SetContent(logo, "Tablet")</c>. Nothing changes if any item is null.
    /// </summary>
    /// <param name="items">The new items. Strings and <see cref="PdfImage"/> values convert to items implicitly.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="items"/> contains a null item.</exception>
    [AddedVersion("0.1.0")]
    [Description("Replaces the cell's whole content with the given items, in order.")]
    public void SetContent(params PdfCellContent[] items)
    {
        SetContent((IEnumerable<PdfCellContent>)items);
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the cell's background color. The default is white.
    /// </summary>
    public PdfColor Background { get; set; } = PdfColor.White;

    /// <summary>
    /// Gets the bottom border.
    /// </summary>
    public PdfBorder BottomBorder { get; } = new PdfBorder();

    /// <summary>
    /// Gets or sets how many columns the cell spans. Spans are not rendered yet.
    /// </summary>
    public int ColSpan { get; set; } = 1;

    /// <summary>
    /// Gets the cell's content items, in drawing order (top to bottom).
    /// </summary>
    public IReadOnlyList<PdfCellContent> Content => _content;

    /// <summary>
    /// Gets or sets the horizontal alignment of every content item. The default is left.
    /// </summary>
    public PdfCellHorizontalAlignment HAlign { get; set; } = PdfCellHorizontalAlignment.Left;

    /// <summary>
    /// Gets whether the cell has nothing to draw: no images and no non-empty text. Trimming removes empty trailing
    /// rows and columns.
    /// </summary>
    internal bool IsEmpty => _content.All(item => item is PdfCellText text && text.Text.Length == 0);

    /// <summary>
    /// Gets the left border. Its color and width are used for all four sides when rendering.
    /// </summary>
    public PdfBorder LeftBorder { get; } = new PdfBorder();

    /// <summary>
    /// Gets or sets the height of each text line as a multiple of its font size. The default is 1.2, so 12 pt text
    /// takes 14.4 pt.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero, negative, or not finite.</exception>
    public double LineHeightFactor
    {
        get => _lineHeightFactor;
        set
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "The line-height factor must be a finite number greater than zero.");
            _lineHeightFactor = value;
        }
    }

    /// <summary>
    /// Gets or sets the space between the cell's border and its content, in points. The default is 4.
    /// </summary>
    public double Padding { get; set; } = 4;

    /// <summary>
    /// Gets the right border.
    /// </summary>
    public PdfBorder RightBorder { get; } = new PdfBorder();

    /// <summary>
    /// Gets or sets how many rows the cell spans. Spans are not rendered yet.
    /// </summary>
    public int RowSpan { get; set; } = 1;

    /// <summary>
    /// Gets or sets the cell's first text item. Reading returns its text, or an empty string when the cell has no
    /// text item. Setting replaces only that item, or appends a text item when there is none; images and other text
    /// items are untouched. Setting null stores an empty string.
    /// </summary>
    public string Text
    {
        get => _content.OfType<PdfCellText>().FirstOrDefault()?.Text ?? string.Empty;
        set
        {
            var item = new PdfCellText(value ?? string.Empty);
            int index = _content.FindIndex(content => content is PdfCellText);
            if (index >= 0)
                _content[index] = item;
            else
                _content.Add(item);
        }
    }

    /// <summary>
    /// Gets the top border.
    /// </summary>
    public PdfBorder TopBorder { get; } = new PdfBorder();

    /// <summary>
    /// Gets or sets the vertical alignment of the whole content block within the row. The default is middle.
    /// </summary>
    public PdfCellVerticalAlignment VAlign { get; set; } = PdfCellVerticalAlignment.Middle;

    #endregion
}
