namespace UnlimitedPDF.Demo.ConsoleApp;

/// <summary>
/// The HTML to PDF demo: converts the sample invoice HTML, whose logo sits next to it in the assets folder, into a PDF.
/// </summary>
public static class HtmlDemo
{
    #region Fields and Constants

    /// <summary>
    /// The sample HTML file in the assets folder.
    /// </summary>
    public const string FileName = "html-invoice.html";

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Loads the sample invoice HTML. Its relative image paths are read from the assets folder.
    /// </summary>
    /// <param name="assetsDirectory">The folder holding the sample HTML and its logo.</param>
    /// <returns>The loaded HTML.</returns>
    public static PdfHtml Load(string assetsDirectory) => PdfHtml.FromFile(Path.Combine(assetsDirectory, FileName));

    /// <summary>
    /// Converts the sample invoice HTML into a PDF file.
    /// </summary>
    /// <param name="assetsDirectory">The folder holding the sample HTML and its logo.</param>
    /// <param name="outputPath">The PDF file to write.</param>
    /// <returns>The loaded HTML, whose warnings list anything that was skipped.</returns>
    public static PdfHtml Write(string assetsDirectory, string outputPath)
    {
        var html = Load(assetsDirectory);
        PdfOperations.ConvertHtml(html, outputPath);
        return html;
    }

    #endregion

    #endregion
}
