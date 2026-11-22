namespace UnlimitedPDF.Demos.BlazorWasm.Services.Tools;

/// <summary>
/// One picture an HTML document names.
/// </summary>
/// <param name="Reference">The reference as written in the HTML, for example "images/logo.png".</param>
/// <param name="FileName">The file name the visitor adds to supply it, for example "logo.png".</param>
/// <param name="IsWebAddress">Whether the reference is a web address, which is never fetched (PR-018).</param>
public sealed record HtmlPicture(string Reference, string FileName, bool IsWebAddress);

/// <summary>
/// Finds the pictures an HTML document names, so the visitor can add them, and supplies the added ones when the HTML is
/// converted. The library reads resources only through the resolver given here: nothing is fetched (PR-018).
/// </summary>
public static class HtmlPictureScan
{
    #region Fields and Constants

    private static readonly string[] PictureExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg", ".bmp"];

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the file name a picture reference is supplied by: its last path part, without a query or fragment,
    /// percent-decoded.
    /// </summary>
    /// <param name="reference">The reference as written in the HTML.</param>
    /// <returns>The file name, for example "logo.png" for "images/logo.png?v=2".</returns>
    public static string FileNameOf(string reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        string path = reference.Trim();
        int cut = path.IndexOfAny(['?', '#']);
        if (cut >= 0) path = path[..cut];
        int slash = path.LastIndexOfAny(['/', '\\']);
        string name = slash >= 0 ? path[(slash + 1)..] : path;
        try
        {
            return Uri.UnescapeDataString(name);
        }
        catch (UriFormatException)
        {
            return name;
        }
    }

    /// <summary>
    /// Returns a resolver that supplies the pictures the visitor added, matched by file name ignoring case, and nothing
    /// for a web address or a picture not added.
    /// </summary>
    /// <param name="added">The added pictures, by file name.</param>
    /// <param name="asked">Receives every reference the HTML asked for.</param>
    /// <returns>The resolver.</returns>
    public static Func<string, Stream?> Resolver(IReadOnlyDictionary<string, byte[]> added, ICollection<string>? asked = null)
    {
        ArgumentNullException.ThrowIfNull(added);
        var byName = new Dictionary<string, byte[]>(added, StringComparer.OrdinalIgnoreCase);
        return reference =>
        {
            asked?.Add(reference);
            if (IsWebAddress(reference)) return null;
            return byName.TryGetValue(FileNameOf(reference), out var bytes) ? new MemoryStream(bytes, writable: false) : null;
        };
    }

    /// <summary>
    /// Lists the pictures <paramref name="html"/> names, in the order it names them, each once.
    /// </summary>
    /// <param name="html">The HTML.</param>
    /// <returns>The pictures; empty when the HTML names none or can't be read.</returns>
    public static IReadOnlyList<HtmlPicture> Scan(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var asked = new List<string>();
        try
        {
            PdfHtml.FromString(html, new PdfHtmlLoadOptions { ResourceResolver = Resolver(new Dictionary<string, byte[]>(), asked) });
        }
        catch (PdfHtmlException)
        {
            return [];
        }

        return asked
            .Where(IsPicture)
            .Distinct(StringComparer.Ordinal)
            .Select(reference => new HtmlPicture(reference, FileNameOf(reference), IsWebAddress(reference)))
            .ToList();
    }

    #endregion

    #region Private Methods

    private static bool IsPicture(string reference)
    {
        string name = FileNameOf(reference);
        return PictureExtensions.Any(ext => name.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsWebAddress(string reference)
    {
        string trimmed = reference.Trim();
        return trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.StartsWith("http:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https:", StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #endregion
}
