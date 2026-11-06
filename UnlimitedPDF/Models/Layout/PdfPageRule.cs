namespace UnlimitedPDF;

/// <summary>
/// Overrides the document's header, footer, body gaps, or side margins on the pages it matches (WR-032). Only the values
/// a rule sets apply. When several rules match a page, the most specific wins for each value: a page-number rule, then
/// First or Last, then Odd or Even, then the document default; between rules of the same kind, the one added later wins.
/// A Last rule may change header and footer content only.
/// </summary>
public sealed class PdfPageRule
{
    #region Fields and Constants

    private readonly Action _layoutChanged;
    private double? _bodyBottomGap;
    private double? _bodyTopGap;
    private double? _leftMargin;
    private double? _rightMargin;

    #endregion

    #region Constructors and Finalizers

    internal PdfPageRule(PdfPageRuleTarget? target, int? pageNumber, int order, Action layoutChanged)
    {
        Target = target;
        PageNumber = pageNumber;
        Order = order;
        _layoutChanged = layoutChanged;
        bool contentOnly = target == PdfPageRuleTarget.Last;
        Header = new PdfPageSection(layoutChanged, contentOnly);
        Footer = new PdfPageSection(layoutChanged, contentOnly);
    }

    #endregion

    #region Methods

    #region Internal Methods

    /// <summary>
    /// Returns whether the rule matches a page.
    /// </summary>
    /// <param name="number">The 1-based page number.</param>
    /// <param name="isLast">Whether the page is the last page; false while the page count is not known.</param>
    /// <returns>Whether the rule applies.</returns>
    internal bool Matches(int number, bool isLast) => Target switch
    {
        PdfPageRuleTarget.First => number == 1,
        PdfPageRuleTarget.Last => isLast,
        PdfPageRuleTarget.Odd => number % 2 == 1,
        PdfPageRuleTarget.Even => number % 2 == 0,
        _ => number == PageNumber
    };

    #endregion

    #region Private Methods

    private static string Sig(double? value) => value is double v ? PdfNumber.Format(v) : "-";

    private double? SetLayout(double? value, string member)
    {
        if (Target == PdfPageRuleTarget.Last)
            throw new InvalidOperationException($"A Last-page rule may change header and footer content only, so its {member} cannot be set: the layout of every page must be known before the last page is (WR-032).");
        if (value is double v) Guard.NonNegative(v, nameof(value));
        _layoutChanged();
        return value;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the gap between the body and the footer band on matching pages, or null to keep the document's.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    /// <exception cref="InvalidOperationException">The rule is a Last rule.</exception>
    public double? BodyBottomGap
    {
        get => _bodyBottomGap;
        set => _bodyBottomGap = SetLayout(value, nameof(BodyBottomGap));
    }

    /// <summary>
    /// Gets or sets the gap between the header band and the body on matching pages, or null to keep the document's.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    /// <exception cref="InvalidOperationException">The rule is a Last rule.</exception>
    public double? BodyTopGap
    {
        get => _bodyTopGap;
        set => _bodyTopGap = SetLayout(value, nameof(BodyTopGap));
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
    /// Gets or sets the left margin on matching pages, or null to keep the document's.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    /// <exception cref="InvalidOperationException">The rule is a Last rule.</exception>
    public double? LeftMargin
    {
        get => _leftMargin;
        set => _leftMargin = SetLayout(value, nameof(LeftMargin));
    }

    /// <summary>
    /// Gets or sets the right margin on matching pages, or null to keep the document's.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    /// <exception cref="InvalidOperationException">The rule is a Last rule.</exception>
    public double? RightMargin
    {
        get => _rightMargin;
        set => _rightMargin = SetLayout(value, nameof(RightMargin));
    }

    /// <summary>
    /// Gets the order the rule was added in, which breaks ties between rules of the same kind.
    /// </summary>
    internal int Order { get; }

    /// <summary>
    /// Gets the page number the rule matches, for a page-number rule.
    /// </summary>
    internal int? PageNumber { get; }

    /// <summary>
    /// Gets how specific the rule is: 3 for a page number, 2 for First or Last, 1 for Odd or Even.
    /// </summary>
    internal int Specificity => Target switch
    {
        null => 3,
        PdfPageRuleTarget.First or PdfPageRuleTarget.Last => 2,
        _ => 1
    };

    /// <summary>
    /// Gets the pages the rule applies to, or null for a page-number rule.
    /// </summary>
    internal PdfPageRuleTarget? Target { get; }

    /// <summary>
    /// Gets a fingerprint of the rule's layout values, used to detect changes after a <c>Body.Remaining</c> query.
    /// </summary>
    internal string LayoutSignature => string.Join(
        "|",
        Target?.ToString() ?? PageNumber!.Value.ToString(CultureInfo.InvariantCulture),
        Sig(Header.HeightSet ? Header.Height : null),
        Header.HiddenSet ? Header.IsHidden.ToString() : "-",
        Sig(Footer.HeightSet ? Footer.Height : null),
        Footer.HiddenSet ? Footer.IsHidden.ToString() : "-",
        Sig(_bodyTopGap),
        Sig(_bodyBottomGap),
        Sig(_leftMargin),
        Sig(_rightMargin));

    #endregion
}
