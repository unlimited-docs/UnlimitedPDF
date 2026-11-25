namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// How the Fonts and languages demo draws its invoice (D-023).
/// </summary>
public enum DrawMode
{
    /// <summary>
    /// With the programmatic calls: <c>AddText</c> and table cells.
    /// </summary>
    TextCalls,

    /// <summary>
    /// With HTML, the fonts registered under CSS family names.
    /// </summary>
    Html
}

/// <summary>
/// The options of the Fonts and languages demo: the drawing mode, the chosen language's invoice fields, which the
/// visitor can edit (D-025), and the extra options (D-029). The fonts live in the page's slots, not here.
/// </summary>
public sealed class FontsAndLanguagesInput
{
    #region Constructors and Finalizers

    /// <summary>
    /// Creates the options with the first preset's invoice.
    /// </summary>
    public FontsAndLanguagesInput() => Apply(LanguageInvoices.All[0]);

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Replaces the invoice fields with a preset's, keeping the mode and the extra options.
    /// </summary>
    /// <param name="preset">The preset.</param>
    public void Apply(LanguageInvoice preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        Language = preset.Id;
        SellerName = preset.SellerName;
        SellerAddress = preset.SellerAddress;
        BuyerName = preset.BuyerName;
        BuyerAddress = preset.BuyerAddress;
        Items.Clear();
        foreach (var (description, quantity, rate) in preset.Items)
            Items.Add(new LanguageInvoiceLine { Description = description, Quantity = quantity, Rate = rate });
    }

    /// <summary>
    /// Returns every text the invoice draws (labels and fields), joined, so the page can tell which scripts it needs.
    /// </summary>
    /// <returns>The text.</returns>
    public string AllText()
    {
        var preset = Preset;
        var labels = preset.Labels;
        return string.Join("\n", new[]
        {
            labels.Title, labels.Number, labels.Date, labels.Seller, labels.Buyer, labels.Item, labels.Quantity,
            labels.Rate, labels.Amount, labels.Total, labels.Thanks, SellerName, SellerAddress, BuyerName, BuyerAddress
        }.Concat(Items.Select(i => i.Description)));
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the body weight for a variable font. The default is 400.
    /// </summary>
    public int BodyWeight { get; set; } = 400;

    /// <summary>
    /// Gets or sets the buyer's address.
    /// </summary>
    public string BuyerAddress { get; set; } = "";

    /// <summary>
    /// Gets or sets the buyer's name.
    /// </summary>
    public string BuyerName { get; set; } = "";

    /// <summary>
    /// Gets or sets whether generating stops on a character no font has, instead of drawing '?'. The default is false.
    /// </summary>
    public bool FailOnMissingCharacters { get; set; }

    /// <summary>
    /// Gets or sets the heading weight for a variable font. The default is 700.
    /// </summary>
    public int HeadingWeight { get; set; } = 700;

    /// <summary>
    /// Gets or sets the invoice date.
    /// </summary>
    public DateTime InvoiceDate { get; set; } = DateTime.Today;

    /// <summary>
    /// Gets or sets the invoice number. The default holds digits only, so it needs no script's font.
    /// </summary>
    public string InvoiceNumber { get; set; } = "2026/0042";

    /// <summary>
    /// Gets the line items. The form adds and removes rows.
    /// </summary>
    public List<LanguageInvoiceLine> Items { get; } = new();

    /// <summary>
    /// Gets or sets the chosen preset's id.
    /// </summary>
    public string Language { get; set; } = "";

    /// <summary>
    /// Gets the language tag of the chosen invoice's text, for the page's <c>lang</c> attributes (WCAG 3.1.2), or null
    /// for the mixed invoice, whose fields hold several languages.
    /// </summary>
    public string? LanguageTag => Language switch
    {
        "hindi" => "hi",
        "telugu" => "te",
        "tamil" => "ta",
        "bengali" => "bn",
        "gujarati" => "gu",
        "punjabi" => "pa",
        "odia" => "or",
        "kannada" => "kn",
        "malayalam" => "ml",
        "french" => "fr",
        "polish" => "pl",
        "czech" => "cs",
        "turkish" => "tr",
        "filipino" => "fil",
        "greek" => "el",
        "russian" => "ru",
        "ukrainian" => "uk",
        "armenian" => "hy",
        "georgian" => "ka",
        "arabic" => "ar",
        "persian" => "fa",
        "urdu" => "ur",
        "hebrew" => "he",
        "dhivehi" => "dv",
        _ => null,
    };

    /// <summary>
    /// Gets or sets the invoice's base direction, or null (the default) for the preset's own: right to left for a
    /// right-to-left invoice, else automatic. The Right-to-left documents demo sets it to show what the setting changes
    /// (WR-041, CR-001).
    /// </summary>
    public PdfTextDirection? Direction { get; set; }

    /// <summary>
    /// Gets the direction the invoice is drawn in: <see cref="Direction"/>, else the preset's.
    /// </summary>
    public PdfTextDirection DrawnDirection => Direction ?? (Preset.RightToLeft ? PdfTextDirection.RightToLeft : PdfTextDirection.Auto);

    /// <summary>
    /// Gets the direction of the chosen invoice's text, for the page's <c>dir</c> attributes: <c>rtl</c> for a
    /// right-to-left invoice, else null, so left-to-right fields keep their markup (WR-041).
    /// </summary>
    public string? TextDirection => Preset.RightToLeft ? "rtl" : null;

    /// <summary>
    /// Gets or sets how the invoice is drawn. The default is the programmatic calls.
    /// </summary>
    public DrawMode Mode { get; set; } = DrawMode.TextCalls;

    /// <summary>
    /// Gets the chosen preset.
    /// </summary>
    public LanguageInvoice Preset => LanguageInvoices.Find(Language);

    /// <summary>
    /// Gets or sets the seller's address.
    /// </summary>
    public string SellerAddress { get; set; } = "";

    /// <summary>
    /// Gets or sets the seller's name.
    /// </summary>
    public string SellerName { get; set; } = "";

    /// <summary>
    /// Gets or sets whether HTML draws a missing bold or italic face synthesized (D-012). The default is true.
    /// </summary>
    public bool SynthesizeMissingStyles { get; set; } = true;

    /// <summary>
    /// Gets the invoice total.
    /// </summary>
    public decimal Total => Items.Sum(i => i.Amount);

    #endregion
}
