namespace UnlimitedPDF;

/// <summary>
/// What a <see cref="PdfHtmlWarning"/> reports: something in the HTML that was skipped or could not be shown as
/// written. The HTML is still converted.
/// </summary>
public enum PdfHtmlWarningKind
{
    /// <summary>
    /// An element the library does not render, such as <c>script</c>, <c>svg</c>, or a form control. Ignored
    /// elements are skipped with their content; other unknown elements keep their content as inline text.
    /// </summary>
    UnsupportedElement,

    /// <summary>
    /// A CSS property the library does not support, such as <c>float</c>, <c>position</c>, or <c>transform</c>.
    /// </summary>
    UnsupportedProperty,

    /// <summary>
    /// A supported CSS property or setting with a value the library does not support, such as <c>display: flex</c>
    /// (laid out as a block) or an unknown character set.
    /// </summary>
    UnsupportedValue,

    /// <summary>
    /// A relative path inside the base folder names a file that does not exist.
    /// </summary>
    ResourceNotFound,

    /// <summary>
    /// A path that leaves the base folder, is absolute, or passes through a symbolic link or junction. It was not read.
    /// </summary>
    ResourceBlocked,

    /// <summary>
    /// A reference the library does not load itself, with no resolver set or a resolver that returned nothing.
    /// </summary>
    ResourceNotResolved,

    /// <summary>
    /// An image in a format other than PNG or JPEG. Its <c>alt</c> text is shown instead.
    /// </summary>
    UnsupportedImageFormat,

    /// <summary>
    /// An image that is damaged or over the pixel limit. Its <c>alt</c> text is shown instead.
    /// </summary>
    InvalidImage,

    /// <summary>
    /// A link that was not made clickable: a scheme other than http, https, or mailto, or a <c>#id</c> with no matching
    /// element. Its text is kept.
    /// </summary>
    LinkDropped,

    /// <summary>
    /// Content still wider than the page body after narrowing, clipped at the body edge.
    /// </summary>
    ContentClipped,

    /// <summary>
    /// A character that no font can draw: not in the text's <c>font-family</c> fonts, not in the fallback fonts, and
    /// not a Western European (WinAnsi) character the built-in fonts have. It is drawn as '?'.
    /// </summary>
    MissingCharacter
}
