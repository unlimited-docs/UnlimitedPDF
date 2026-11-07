namespace UnlimitedPDF;

/// <summary>
/// Page settings: overrides of the document's header, footer, body margins, or side margins on the pages they match
/// (WR-032). Only the values they set apply. When several page settings match a page, the most specific wins for each
/// value: page-number settings, then First or Last, then Odd or Even, then the document default; between page settings
/// of the same kind, the ones added later win. Last-page settings may change header and footer content only.
/// </summary>
public sealed class PdfPageSettings
{
    #region Fields and Constants

    private readonly Action _layoutChanged;
    private double? _bodyMarginBottom;
    private double? _bodyMarginTop;
    private double? _marginLeft;
    private double? _marginRight;

    #endregion

    #region Constructors and Finalizers

    internal PdfPageSettings(PdfPageTarget? target, int? pageNumber, int order, Action layoutChanged)
    {
        Target = target;
        PageNumber = pageNumber;
        Order = order;
        _layoutChanged = layoutChanged;
        bool contentOnly = target == PdfPageTarget.Last;
        Header = new PdfPageSection(layoutChanged, contentOnly);
        Footer = new PdfPageSection(layoutChanged, contentOnly);
    }

    #endregion

    #region Methods

    #region Internal Methods

    /// <summary>
    /// Returns whether the page settings match a page.
    /// </summary>
    /// <param name="number">The 1-based page number.</param>
    /// <param name="isLast">Whether the page is the last page; false while the page count is not known.</param>
    /// <returns>Whether the page settings apply.</returns>
    internal bool Matches(int number, bool isLast) => Target switch
    {
        PdfPageTarget.First => number == 1,
        PdfPageTarget.Last => isLast,
        PdfPageTarget.Odd => number % 2 == 1,
        PdfPageTarget.Even => number % 2 == 0,
        _ => number == PageNumber
    };

    #endregion

    #region Private Methods

    private static string Sig(double? value) => value is double v ? PdfNumber.Format(v) : "-";

    private double? SetLayout(double? value, string member)
    {
        if (Target == PdfPageTarget.Last)
            throw new InvalidOperationException($"Last-page settings may change header and footer content only, so its {member} cannot be set: the layout of every page must be known before the last page is (WR-032).");
        if (value is double v) Guard.NonNegative(v, nameof(value));
        _layoutChanged();
        return value;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the space between the body and the footer band on matching pages, or null to keep the document's.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    /// <exception cref="InvalidOperationException">These are Last-page settings.</exception>
    [AddedVersion("1.0.0")]
    public double? BodyMarginBottom
    {
        get => _bodyMarginBottom;
        set => _bodyMarginBottom = SetLayout(value, nameof(BodyMarginBottom));
    }

    /// <summary>
    /// Gets or sets the space between the header band and the body on matching pages, or null to keep the document's.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    /// <exception cref="InvalidOperationException">These are Last-page settings.</exception>
    [AddedVersion("1.0.0")]
    public double? BodyMarginTop
    {
        get => _bodyMarginTop;
        set => _bodyMarginTop = SetLayout(value, nameof(BodyMarginTop));
    }

    /// <summary>
    /// Gets the footer overrides for matching pages.
    /// </summary>
    public PdfPageSection Footer { get; }

    /// <summary>
    /// Gets the header overrides for matching pages.
    /// </summary>
    public PdfPageSection Header { get; }

    /// <summary>
    /// Gets a fingerprint of the layout values, used to detect changes after a <c>Body.RemainingHeight</c> query.
    /// </summary>
    internal string LayoutSignature => string.Join(
        "|",
        Target?.ToString() ?? PageNumber!.Value.ToString(CultureInfo.InvariantCulture),
        Sig(Header.HeightSet ? Header.Height : null),
        Header.HiddenSet ? Header.IsHidden.ToString() : "-",
        Sig(Footer.HeightSet ? Footer.Height : null),
        Footer.HiddenSet ? Footer.IsHidden.ToString() : "-",
        Sig(_bodyMarginTop),
        Sig(_bodyMarginBottom),
        Sig(_marginLeft),
        Sig(_marginRight));

    /// <summary>
    /// Gets or sets the left margin on matching pages, or null to keep the document's.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    /// <exception cref="InvalidOperationException">These are Last-page settings.</exception>
    [AddedVersion("1.0.0")]
    public double? MarginLeft
    {
        get => _marginLeft;
        set => _marginLeft = SetLayout(value, nameof(MarginLeft));
    }

    /// <summary>
    /// Gets or sets the right margin on matching pages, or null to keep the document's.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    /// <exception cref="InvalidOperationException">These are Last-page settings.</exception>
    [AddedVersion("1.0.0")]
    public double? MarginRight
    {
        get => _marginRight;
        set => _marginRight = SetLayout(value, nameof(MarginRight));
    }

    /// <summary>
    /// Gets the order the page settings were added in, which breaks ties between page settings of the same kind.
    /// </summary>
    internal int Order { get; }

    /// <summary>
    /// Gets the page number the page settings match, for page-number settings.
    /// </summary>
    internal int? PageNumber { get; }

    /// <summary>
    /// Gets how specific the page settings are: 3 for a page number, 2 for First or Last, 1 for Odd or Even.
    /// </summary>
    internal int Specificity => Target switch
    {
        null => 3,
        PdfPageTarget.First or PdfPageTarget.Last => 2,
        _ => 1
    };

    /// <summary>
    /// Gets the pages the page settings apply to, or null for page-number settings.
    /// </summary>
    internal PdfPageTarget? Target { get; }

    #endregion
}
