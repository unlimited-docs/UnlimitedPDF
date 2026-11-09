namespace UnlimitedPDF;

/// <summary>
/// The reason a <see cref="PdfHtmlException"/> was thrown.
/// </summary>
public enum PdfHtmlErrorReason
{
    /// <summary>
    /// The HTML input is larger than the input size limit.
    /// </summary>
    InputTooLarge,

    /// <summary>
    /// Elements are nested deeper than the nesting depth limit.
    /// </summary>
    NestingTooDeep,

    /// <summary>
    /// One image or stylesheet is larger than the per-resource size limit.
    /// </summary>
    ResourceTooLarge,

    /// <summary>
    /// The images and stylesheets of one HTML input together are larger than the total resource size limit.
    /// </summary>
    ResourcesTooLarge,

    /// <summary>
    /// <see cref="PdfHtmlLoadOptions.FailOnWarning"/> is set and a warning occurred; see <see cref="PdfHtmlException.Warning"/>.
    /// </summary>
    Warning
}
