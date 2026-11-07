namespace UnlimitedPDF;

/// <summary>
/// A header or footer band (WR-030, WR-031): a fixed height, whether it is hidden, and its content, drawn on every saved
/// page. In page settings, only the values they set apply; content added to their section replaces the document's
/// content on the pages they match.
/// </summary>
public sealed class PdfPageSection : PdfSectionContent
{
    #region Fields and Constants

    private readonly Action _layoutChanged;
    private readonly bool _contentOnly;
    private Action<PdfSectionContent, PdfPageInfo>? _build;
    private double _height;
    private bool _isHidden;

    #endregion

    #region Constructors and Finalizers

    internal PdfPageSection(Action layoutChanged, bool contentOnly = false)
    {
        _layoutChanged = layoutChanged;
        _contentOnly = contentOnly;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Sets a callback that adds content for each saved page at save, after the content added directly. It receives
    /// the page number, the page count, and whether the page is first or last. A later call replaces the callback.
    /// </summary>
    /// <param name="build">The callback.</param>
    /// <exception cref="ArgumentNullException"><paramref name="build"/> is null.</exception>
    [AddedVersion("1.0.0")]
    [Description("Sets a callback that builds the band's content for each saved page at save.")]
    public void SetPageContent(Action<PdfSectionContent, PdfPageInfo> build)
    {
        _build = build ?? throw new ArgumentNullException(nameof(build));
    }

    #endregion

    #region Private Methods

    private void EnsureLayoutAllowed(string member)
    {
        if (_contentOnly)
            throw new InvalidOperationException($"Last-page settings may change header and footer content only, so its {member} cannot be set: the layout of every page must be known before the last page is (WR-032).");
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the band height in points. The default is 0. A band of height 0 takes no space and draws nothing.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    /// <exception cref="InvalidOperationException">The section belongs to Last-page settings.</exception>
    public double Height
    {
        get => _height;
        set
        {
            EnsureLayoutAllowed(nameof(Height));
            Guard.NonNegative(value, nameof(value));
            _height = value;
            HeightSet = true;
            _layoutChanged();
        }
    }

    /// <summary>
    /// Gets or sets whether the band is hidden. A hidden band gives its space to the body. The default is false.
    /// </summary>
    /// <exception cref="InvalidOperationException">The section belongs to Last-page settings.</exception>
    public bool IsHidden
    {
        get => _isHidden;
        set
        {
            EnsureLayoutAllowed(nameof(IsHidden));
            _isHidden = value;
            HiddenSet = true;
            _layoutChanged();
        }
    }

    /// <summary>
    /// Gets the per-page content callback, or null.
    /// </summary>
    internal Action<PdfSectionContent, PdfPageInfo>? Callback => _build;

    /// <summary>
    /// Gets whether content was added or a callback set.
    /// </summary>
    internal bool HasContent => Items.Count > 0 || _build is not null;

    /// <summary>
    /// Gets whether <see cref="Height"/> was set.
    /// </summary>
    internal bool HeightSet { get; private set; }

    /// <summary>
    /// Gets whether <see cref="IsHidden"/> was set.
    /// </summary>
    internal bool HiddenSet { get; private set; }

    #endregion
}
