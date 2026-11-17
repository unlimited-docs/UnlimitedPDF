namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// The variants of the demos that have them: the document types the Invoice demo makes and the Document packs demo's
/// domains. The invoice types come from the catalog, so each keeps its own catalog address, title, and description.
/// </summary>
public static class DemoVariants
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Finds the variant an address names, ignoring case and surrounding spaces.
    /// </summary>
    /// <param name="variants">The demo's variants.</param>
    /// <param name="value">The value from the address, or null when the address has none.</param>
    /// <returns>The variant, or the first one for a missing or unknown value.</returns>
    public static DemoVariant Find(IReadOnlyList<DemoVariant> variants, string? value) =>
        variants.FirstOrDefault(v => string.Equals(v.Value, value?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? variants[0];

    /// <summary>
    /// Tells whether a demo page has moved from one of its variants to another, by a link, Back, or Forward: its form
    /// is then replaced, and the choice's shown link takes focus.
    /// </summary>
    /// <param name="previous">The variant the page showed before, or null when the page has just opened.</param>
    /// <param name="current">The variant the page shows now.</param>
    /// <returns>True when the page showed a different variant before.</returns>
    public static bool Moved(DemoVariant? previous, DemoVariant current) => previous is not null && previous != current;

    #endregion

    #region Private Methods

    private static string QueryValue(string route, string name)
    {
        string prefix = "?" + name + "=";
        int start = route.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0) throw new InvalidOperationException($"'{route}' has no {name}.");
        return route[(start + prefix.Length)..];
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the document types of the Invoice demo: the invoice, then <see cref="SampleCatalog.InvoiceTypes"/>.
    /// </summary>
    public static IReadOnlyList<DemoVariant> InvoiceTypes { get; } =
    [
        new("invoice", SampleCatalog.Invoice.Title, SampleCatalog.Invoice.Route),
        .. SampleCatalog.InvoiceTypes.Select(d => new DemoVariant(QueryValue(d.Route, "type"), d.Title, d.Route)),
    ];

    /// <summary>
    /// Gets the Document packs demo's domains, the finance pack first.
    /// </summary>
    public static IReadOnlyList<DemoVariant> PackDomains { get; } =
    [
        new("finance", "Finance: month end", SampleCatalog.DocumentPacks.Route),
        new("hr", "HR: employee", SampleCatalog.DocumentPacks.Route + "?domain=hr"),
        new("healthcare", "Healthcare: patient", SampleCatalog.DocumentPacks.Route + "?domain=healthcare"),
    ];

    #endregion
}
