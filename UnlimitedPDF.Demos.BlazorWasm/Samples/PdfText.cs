namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Prepares text for <see cref="PdfPageBuilder.AddText"/>, which writes its text into a PDF string as given.
/// </summary>
public static class PdfText
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Escapes the characters that would otherwise end or change a PDF string: backslash and both parentheses. The
    /// PDF still shows the text exactly as typed.
    /// </summary>
    /// <param name="text">The text to escape.</param>
    /// <returns>The escaped text, for example <c>Total \(net\)</c> for <c>Total (net)</c>.</returns>
    public static string Escape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }

    #endregion

    #endregion
}
