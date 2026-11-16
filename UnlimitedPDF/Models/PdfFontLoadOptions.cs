namespace UnlimitedPDF;

/// <summary>
/// Options for loading a font with <see cref="PdfFont"/>.
/// </summary>
public sealed class PdfFontLoadOptions
{
    #region Fields and Constants

    private long? _maxFileSize;
    private int? _weight;

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets whether a font whose embedding permissions (<c>OS/2</c> <c>fsType</c>) forbid embedding, or allow
    /// only bitmaps, is loaded anyway. Set it only when you hold the rights to embed the font (PR-022). The default is
    /// false. A font that forbids subsetting is always embedded whole.
    /// </summary>
    public bool IgnoreEmbeddingRestrictions { get; set; }

    /// <summary>
    /// Gets or sets the file size limit for this load, in bytes, or null to use <see cref="PdfFont.DefaultMaxFileSize"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public long? MaxFileSize
    {
        get => _maxFileSize;
        set
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), value, "The file size limit must be greater than zero.");
            _maxFileSize = value;
        }
    }

    /// <summary>
    /// Gets or sets the weight to draw with, from 1 to 1000 (400 is regular, 700 is bold), or null for the font's own
    /// weight. A variable font draws at this weight, limited to the weights it holds; a font with one weight fails to
    /// load when this differs from that weight.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside 1 to 1000.</exception>
    public int? Weight
    {
        get => _weight;
        set
        {
            if (value is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(value), value, "The weight must be from 1 to 1000.");
            _weight = value;
        }
    }

    #endregion
}
