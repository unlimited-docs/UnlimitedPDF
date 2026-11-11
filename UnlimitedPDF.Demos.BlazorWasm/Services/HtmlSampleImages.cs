namespace UnlimitedPDF.Demos.BlazorWasm.Services;

/// <summary>
/// Supplies the images the HTML to PDF demos name ("logo.png" and "banner.png") from the app's own <c>images/</c>
/// folder, as the resolver the HTML is loaded with. Any other reference returns null, so the library reports it and
/// shows the image's alt text; nothing is fetched from anywhere else.
/// </summary>
public sealed class HtmlSampleImages
{
    #region Fields and Constants

    private readonly HttpClient _http;

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Initializes a new instance of the <see cref="HtmlSampleImages"/> class.
    /// </summary>
    /// <param name="http">The client for the app's own origin.</param>
    public HtmlSampleImages(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the image an HTML document names, or null when it is not one of the demos' images.
    /// </summary>
    /// <param name="reference">The reference as written in the HTML.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>A stream over the image bytes, or null.</returns>
    public async Task<Stream?> ResolveAsync(string reference, CancellationToken cancellationToken)
    {
        string? path = reference switch
        {
            "logo.png" => "images/invoice-logo.png",
            "banner.png" => "images/unlimitedpdf-demos-hero-800.jpg",
            _ => null
        };
        return path is null ? null : new MemoryStream(await _http.GetByteArrayAsync(path, cancellationToken));
    }

    #endregion

    #endregion
}
