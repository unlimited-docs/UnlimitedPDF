namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// The demos site's public address, the one place it is written in code. Canonical links use it; the static files
/// <c>sitemap.xml</c>, <c>robots.txt</c>, and <c>llms.txt</c> in <c>wwwroot</c> repeat it, and tests keep them in step.
/// </summary>
public static class SiteLinks
{
    #region Fields and Constants

    /// <summary>
    /// The site's address, ending in a slash.
    /// </summary>
    public const string Address = "https://unlimitedpdf.dev/";

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the absolute address of a page on the site.
    /// </summary>
    /// <param name="path">The page's path without a leading slash, for example "faq"; an empty path is the home page.</param>
    /// <returns>The absolute address, for example "https://unlimitedpdf.dev/faq".</returns>
    public static string For(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Address + path.TrimStart('/');
    }

    #endregion

    #endregion
}
