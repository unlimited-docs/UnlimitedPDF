namespace UnlimitedPDF;

/// <summary>
/// Options for one image load. New options are added as properties, so existing code keeps compiling.
/// </summary>
public sealed class PdfImageLoadOptions
{
    #region Fields and Constants

    private long? _maxPixels;

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets whether a JPEG's EXIF orientation is honored, so photos appear upright as captured and report
    /// their displayed size. The default is true. The JPEG bytes are embedded unchanged either way.
    /// </summary>
    public bool ApplyExifOrientation { get; set; } = true;

    /// <summary>
    /// Gets or sets how the values of a CMYK JPEG are interpreted. The default is <see cref="PdfCmykInversion.Auto"/>.
    /// </summary>
    public PdfCmykInversion CmykInversion { get; set; } = PdfCmykInversion.Auto;

    /// <summary>
    /// Gets or sets the most pixels (width x height) this load accepts. When null, <see cref="PdfImage.DefaultMaxPixels"/>
    /// applies. A value here wins over the process-wide default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public long? MaxPixels
    {
        get => _maxPixels;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "The pixel limit must be greater than zero.");
            _maxPixels = value;
        }
    }

    #endregion
}
