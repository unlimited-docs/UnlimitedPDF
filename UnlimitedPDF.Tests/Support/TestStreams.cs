namespace UnlimitedPDF.Tests.Support;

/// <summary>
/// A readable stream that cannot seek and reports its own disposal, like a network or request stream.
/// </summary>
internal sealed class NonSeekableStream : Stream
{
    #region Fields and Constants

    private readonly MemoryStream _inner;

    #endregion

    #region Constructors and Finalizers

    public NonSeekableStream(byte[] data) => _inner = new MemoryStream(data);

    #endregion

    #region Methods

    #region Public Methods

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    #endregion

    #region Protected Methods

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        base.Dispose(disposing);
    }

    #endregion

    #endregion

    #region Properties, Indexers

    public override bool CanRead => !IsDisposed;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public bool IsDisposed { get; private set; }

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    #endregion
}

/// <summary>
/// A stream whose asynchronous reads wait until <see cref="Release"/> is called, honoring cancellation while waiting.
/// </summary>
internal sealed class GatedStream : Stream
{
    #region Fields and Constants

    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly MemoryStream _inner;

    #endregion

    #region Constructors and Finalizers

    public GatedStream(byte[] data) => _inner = new MemoryStream(data);

    #endregion

    #region Methods

    #region Public Methods

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Use ReadAsync.");

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await _gate.Task.WaitAsync(cancellationToken);
        return await _inner.ReadAsync(buffer, cancellationToken);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public void Release() => _gate.TrySetResult();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    #endregion

    #endregion

    #region Properties, Indexers

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    #endregion
}
