using UnlimitedPDF.Models.Table;

namespace UnlimitedPDF;

/// <summary>
/// Manages the content for a PDF page, allowing for the addition of elements like text.
/// </summary>
public class PdfPageBuilder
{
    #region Fields and Constants

    private readonly List<object> _contentElements = new();
    private readonly PdfPageContentStream _contentStream;
    private readonly int _pageNumber;

    #endregion

    #region Constructors and Finalizers

    internal PdfPageBuilder(PdfPageContentStream contentStream, int pageNumber = 1, int continuationIndex = 0)
    {
        _contentStream = contentStream;
        _pageNumber = pageNumber;
        ContinuationIndex = continuationIndex;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Adds an image to the page with its bottom-left corner at (<paramref name="x"/>, <paramref name="y"/>), in points
    /// from the page's bottom-left corner. With a width and a height the image fills that box (see
    /// <see cref="PdfImagePlacementOptions.Fit"/>); with one of them the other follows the image's aspect ratio; with
    /// neither the image is drawn at 96 pixels per inch (1 px = 0.75 pt). Content is drawn in the order it is added.
    /// </summary>
    /// <param name="image">The image to draw. One image can be placed any number of times and is stored once.</param>
    /// <param name="x">The x-coordinate of the image's bottom-left corner, in points. Off-page values are allowed.</param>
    /// <param name="y">The y-coordinate of the image's bottom-left corner, in points. Off-page values are allowed.</param>
    /// <param name="width">The width in points, or null.</param>
    /// <param name="height">The height in points, or null.</param>
    /// <param name="options">Placement options, or null for the defaults.</param>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A coordinate is not finite, or a size is zero, negative, or not finite.</exception>
    [AddedVersion("0.1.0")]
    [Description("Adds an image to the page with its bottom-left corner at the given position, in points.")]
    public void AddImage(PdfImage image, double x, double y, double? width = null, double? height = null, PdfImagePlacementOptions? options = null)
    {
        if (image is null) throw new ArgumentNullException(nameof(image));
        EnsureFinite(x, nameof(x));
        EnsureFinite(y, nameof(y));
        if (width is double w) EnsurePositive(w, nameof(width));
        if (height is double h) EnsurePositive(h, nameof(height));

        var box = ImageBox.Compute(image, width, height, options);
        double drawX = x + box.OffsetX, drawY = y + box.OffsetY, drawWidth = box.DrawWidth, drawHeight = box.DrawHeight;

        _contentElements.Add(new PdfImageElement(image.Data, drawX, drawY, drawWidth, drawHeight));
    }

    /// <summary>
    /// Adds a filled rectangle with its bottom-left corner at (<paramref name="x"/>, <paramref name="y"/>), in points from
    /// the page's bottom-left corner, for example a rule, a band, or a box. A rectangle never continues onto another
    /// page and is drawn in the order it is added.
    /// </summary>
    /// <param name="x">The x-coordinate of the bottom-left corner, in points.</param>
    /// <param name="y">The y-coordinate of the bottom-left corner, in points.</param>
    /// <param name="width">The width in points.</param>
    /// <param name="height">The height in points.</param>
    /// <param name="color">The fill color.</param>
    /// <exception cref="ArgumentNullException"><paramref name="color"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A coordinate is not finite, or a size is zero, negative, or not finite.</exception>
    [AddedVersion("0.1.0")]
    [Description("Adds a filled rectangle to the page with its bottom-left corner at the given position, in points.")]
    public void AddRectangle(double x, double y, double width, double height, PdfColor color)
    {
        if (color is null) throw new ArgumentNullException(nameof(color));
        EnsureFinite(x, nameof(x));
        EnsureFinite(y, nameof(y));
        EnsurePositive(width, nameof(width));
        EnsurePositive(height, nameof(height));
        _contentElements.Add(new PdfRectangleElement(x, y, width, height, color));
    }

    /// <summary>
    /// Adds a new table to the page at the specified position and with the given width.
    /// </summary>
    /// <param name="x">The x-coordinate of the table's top-left corner.</param>
    /// <param name="y">The y-coordinate of the table's top-left corner.</param>
    /// <param name="width">The total width of the table.</param>
    /// <returns>A <see cref="PdfTable"/> object for configuration.</returns>
    public PdfTable AddTable(double x, double y, double width)
    {
        var table = new PdfTable
        {
            X = x,
            Y = y,
            Width = width
        };
        _contentElements.Add(table);
        return table;
    }

    /// <summary>
    /// Adds a text element to the content at the specified position with the given font settings.
    /// </summary>
    /// <param name="text">The text to be added to the content. Cannot be null.</param>
    /// <param name="x">The x-coordinate of the text's position, in points.</param>
    /// <param name="y">The y-coordinate of the text's position, in points.</param>
    /// <param name="fontSize">The size of the font, in points. The default value is 12.</param>
    /// <param name="fontName">The name of the font to use. The default value is "F1".</param>
    public void AddText(string text, int x, int y, int fontSize = 12, string fontName = "F1")
    {
        _contentElements.Add(new PdfTextElement
        {
            Text = text,
            X = x,
            Y = y,
            FontSize = fontSize,
            FontName = fontName
        });
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Compiles the content elements into content stream bytes without changing the page, drawing every table whole
    /// on this page. Layout errors are thrown here, before the document commits anything.
    /// </summary>
    /// <returns>The compiled content and the distinct images it draws, in resource-name order.</returns>
    internal PdfPageContent BuildContent() => BuildContent(plans: null, carriedParts: Array.Empty<(PdfTable, TablePart)>(), after: null);

    /// <summary>
    /// Compiles the page's content into content stream bytes without changing the page: first the parts of tables
    /// from earlier pages that continue here, then the page's own elements in the order they were added, each of
    /// its tables drawn as its first part when <paramref name="plans"/> is given.
    /// </summary>
    /// <param name="plans">Each table's page plan, or null to draw every table whole on this page.</param>
    /// <param name="carriedParts">Parts of tables from earlier pages that are drawn on this page, in drawing order.</param>
    /// <param name="after">Draws the page's body flow content and its header and footer after the page's own content,
    /// or null.</param>
    /// <returns>The compiled content and the distinct images it draws, in resource-name order.</returns>
    internal PdfPageContent BuildContent(
        IReadOnlyDictionary<PdfTable, TablePlan>? plans,
        IReadOnlyList<(PdfTable Table, TablePart Part)> carriedParts,
        Action<StringBuilder, Func<PdfImageData, string>>? after)
    {
        var sb = new StringBuilder();
        var images = new List<PdfImageData>();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        // Identical images, on the page or in its tables, share one resource name and so one stored object.
        string NameFor(PdfImageData image)
        {
            if (!names.TryGetValue(image.ContentKey, out var name))
            {
                images.Add(image);
                name = $"/Im{images.Count}";
                names[image.ContentKey] = name;
            }
            return name;
        }

        foreach (var (carriedTable, part) in carriedParts)
        {
            sb.Append(PdfTableRenderer.RenderPart(carriedTable, plans![carriedTable], part, NameFor));
        }

        int tableNumber = 0;
        foreach (var element in _contentElements)
        {
            if (element is PdfTable table)
            {
                if (plans is null)
                {
                    sb.Append(PdfTableRenderer.Render(table, _pageNumber, ++tableNumber, NameFor));
                }
                else
                {
                    var plan = plans[table];
                    sb.Append(PdfTableRenderer.RenderPart(table, plan, plan.Parts[0], NameFor));
                }
            }
            else if (element is PdfImageElement image)
            {
                sb.AppendLine(image.ToContent(NameFor(image.Image)));
            }
            else
            {
                sb.AppendLine(element.ToString());
            }
        }
        after?.Invoke(sb, NameFor);
        return new PdfPageContent(Encoding.ASCII.GetBytes(sb.ToString().Trim()), images);
    }

    /// <summary>
    /// Stores compiled content in the page's content stream.
    /// </summary>
    /// <param name="content">Bytes from <see cref="BuildContent"/>.</param>
    internal void CommitContent(byte[] content)
    {
        _contentStream.StreamData = content;
    }

    #endregion

    #region Private Methods

    private static void EnsureFinite(double value, string paramName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(paramName, value, "The value must be a finite number.");
    }

    private static void EnsurePositive(double value, string paramName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            throw new ArgumentOutOfRangeException(paramName, value, "The size must be a finite number greater than zero.");
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the page's position among the continuation pages of its caller page (1 for the first), or 0 for a page
    /// the caller added.
    /// </summary>
    internal int ContinuationIndex { get; }

    /// <summary>
    /// Gets a description of the page for error messages, for example "page 2" or "continuation page 1 of page 2".
    /// </summary>
    internal string Description => ContinuationIndex == 0
        ? string.Format(CultureInfo.InvariantCulture, "page {0}", _pageNumber)
        : string.Format(CultureInfo.InvariantCulture, "continuation page {0} of page {1}", ContinuationIndex, _pageNumber);

    /// <summary>
    /// Gets the 1-based number of the caller page this page is, or continues.
    /// </summary>
    internal int PageNumber => _pageNumber;

    /// <summary>
    /// Gets the tables added to this page, in the order they were added.
    /// </summary>
    internal IEnumerable<PdfTable> Tables => _contentElements.OfType<PdfTable>();

    #endregion
}
