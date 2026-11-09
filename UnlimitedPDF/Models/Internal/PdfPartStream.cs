namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// A write-only view of a caller's stream for one split part (WR-037). <see cref="Position"/> counts the bytes written
/// through this view, so the part's cross-reference offsets start at its first byte whatever the stream held before,
/// and streams that cannot seek or report a position (for example zip entries) can be written to. Disposing it leaves
/// the caller's stream open.
/// </summary>
internal sealed class PdfPartStream : Stream
{
    #region Fields and Constants

    private readonly Stream _inner;
    private long _written;

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Initializes a view that writes to <paramref name="inner"/>.
    /// </summary>
    /// <param name="inner">The caller's stream.</param>
    public PdfPartStream(Stream inner) => _inner = inner;

    #endregion

    #region Methods

    #region Public Methods

    /// <inheritdoc />
    public override void Flush() => _inner.Flush();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        _inner.Write(buffer, offset, count);
        _written += count;
    }

    /// <inheritdoc />
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await _inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        _written += count;
    }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await _inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        _written += buffer.Length;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => _inner.CanWrite;

    /// <inheritdoc />
    public override long Length => _written;

    /// <summary>
    /// Gets the number of bytes written through this view; setting it is not supported.
    /// </summary>
    public override long Position
    {
        get => _written;
        set => throw new NotSupportedException();
    }

    #endregion
}
