namespace UnlimitedPDF;

/// <summary>
/// Options for one load of an existing PDF. A value set here wins over the process-wide default on
/// <see cref="PdfFile"/>; new options are added as properties, so existing code keeps compiling.
/// </summary>
public sealed class PdfFileLoadOptions
{
    #region Fields and Constants

    private long? _maxFileSize;
    private int? _maxObjectCount;
    private long? _maxStreamSize;

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the most bytes the input may have. When null, <see cref="PdfFile.DefaultMaxFileSize"/> applies.
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
    /// Gets or sets the most objects the input may list. When null, <see cref="PdfFile.DefaultMaxObjectCount"/>
    /// applies.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int? MaxObjectCount
    {
        get => _maxObjectCount;
        set
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), value, "The object limit must be greater than zero.");
            _maxObjectCount = value;
        }
    }

    /// <summary>
    /// Gets or sets the most bytes one compressed stream may decompress to. When null,
    /// <see cref="PdfFile.DefaultMaxStreamSize"/> applies.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public long? MaxStreamSize
    {
        get => _maxStreamSize;
        set
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), value, "The stream size limit must be greater than zero.");
            _maxStreamSize = value;
        }
    }

    #endregion
}
