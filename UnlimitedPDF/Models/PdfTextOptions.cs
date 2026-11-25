namespace UnlimitedPDF;

/// <summary>
/// Settings for one text call (<c>AddText</c> on a page, the body flow, a flow block, or a header or footer). New
/// settings are added as properties, so existing code keeps compiling. One instance can be reused for many calls.
/// </summary>
public sealed class PdfTextOptions
{
    #region Fields and Constants

    private PdfTextDirection? _direction;

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the text's base direction, or null (the default) to use the document's
    /// <see cref="PdfDocument.TextDirection"/> (WR-041).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined <see cref="PdfTextDirection"/>.</exception>
    [AddedVersion("1.2.0")]
    public PdfTextDirection? Direction
    {
        get => _direction;
        set => _direction = value is { } direction ? TextDirections.Checked(direction, nameof(value)) : null;
    }

    #endregion
}
