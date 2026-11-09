namespace UnlimitedPDF.Tests.Support;

/// <summary>
/// Wraps a real file stream and throws <see cref="IOException"/> once more than a set number of bytes would be
/// written, or waits for cancellation in an asynchronous write, so folder-output tests can fail a part on disk (V-10).
/// Disposing it disposes the file stream, then throws when set to, the way a file stream that failed to write throws
/// again when it flushes on dispose (review F-01).
/// </summary>
internal sealed class FailingFileStream : Stream
{
    #region Fields and Constants

    private readonly Stream _inner;
    private readonly long? _failAfterBytes;
    private readonly bool _failOnDispose;
    private readonly long? _pauseAfterBytes;
    private long _written;

    #endregion

    #region Constructors and Finalizers

    public FailingFileStream(string path, long? failAfterBytes = null, long? pauseAfterBytes = null, bool failOnDispose = false)
    {
        _inner = new FileStream(path, FileMode.Create, FileAccess.Write);
        _failAfterBytes = failAfterBytes;
        _pauseAfterBytes = pauseAfterBytes;
        _failOnDispose = failOnDispose;
    }

    #endregion

    #region Methods

    #region Public Methods

    public override void Flush() => _inner.Flush();

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (_failAfterBytes is not null && _written + count > _failAfterBytes) throw new IOException("Disk full.");
        _inner.Write(buffer, offset, count);
        _written += count;
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_pauseAfterBytes is not null && _written >= _pauseAfterBytes)
        {
            Paused.TrySetResult();
            await Resume.Task.WaitAsync(cancellationToken);
        }
        Write(buffer, offset, count);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        new(WriteAsync(buffer.ToArray(), 0, buffer.Length, cancellationToken));

    #endregion

    #region Protected Methods

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
        if (disposing && _failOnDispose) throw new IOException("Disk full while closing.");
    }

    #endregion

    #endregion

    #region Properties, Indexers

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    /// <summary>
    /// Gets a task that completes when an asynchronous write pauses.
    /// </summary>
    public TaskCompletionSource Paused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Gets the gate a paused write waits on, besides its own cancellation token; complete it to let writing go on.
    /// </summary>
    public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    #endregion
}

/// <summary>
/// A write-only, non-seekable stream that collects its bytes, logs when it is written and disposed, and can fail,
/// wait part-way through a write, or reject synchronous I/O like a web response body (split tests, V-06, V-07, V-10,
/// review F-02).
/// </summary>
internal sealed class SplitTargetStream : Stream
{
    #region Fields and Constants

    private readonly MemoryStream _inner = new();
    private readonly List<string>? _log;
    private readonly string _name;

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Initializes a stream named <paramref name="name"/> that appends "write name" and "dispose name" to
    /// <paramref name="log"/>.
    /// </summary>
    public SplitTargetStream(string name, List<string>? log = null)
    {
        _name = name;
        _log = log;
    }

    #endregion

    #region Methods

    #region Public Methods

    public override void Flush()
    {
        if (AsyncOnly) throw new InvalidOperationException("Synchronous operations are disallowed.");
    }

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    /// <summary>
    /// Returns the bytes written so far.
    /// </summary>
    public byte[] ToArray() => _inner.ToArray();

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (AsyncOnly) throw new InvalidOperationException("Synchronous operations are disallowed.");
        WriteCore(buffer, offset, count);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (PauseAfterBytes is not null && _inner.Length >= PauseAfterBytes)
        {
            Paused.TrySetResult();
            await Resume.Task.WaitAsync(cancellationToken);
        }
        WriteCore(buffer, offset, count);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        new(WriteAsync(buffer.ToArray(), 0, buffer.Length, cancellationToken));

    #endregion

    #region Protected Methods

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed) _log?.Add("dispose " + _name);
        IsDisposed = true;
        base.Dispose(disposing);
    }

    #endregion

    #region Private Methods

    private void WriteCore(byte[] buffer, int offset, int count)
    {
        if (IsDisposed) throw new ObjectDisposedException(_name);
        if (_inner.Length == 0) _log?.Add("write " + _name);
        if (FailAfterBytes is not null && _inner.Length + count > FailAfterBytes) throw new IOException("Disk full.");
        _inner.Write(buffer, offset, count);
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets whether synchronous <see cref="Write"/> and <see cref="Flush"/> throw, as on a web response body
    /// that allows only asynchronous I/O.
    /// </summary>
    public bool AsyncOnly { get; set; }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => !IsDisposed;

    /// <summary>
    /// Gets or sets the byte count after which a write throws <see cref="IOException"/>, or null to never fail.
    /// </summary>
    public long? FailAfterBytes { get; set; }

    public bool IsDisposed { get; private set; }

    public override long Length => throw new NotSupportedException();

    /// <summary>
    /// Gets a task that completes when an asynchronous write pauses at <see cref="PauseAfterBytes"/>.
    /// </summary>
    public TaskCompletionSource Paused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Gets or sets the byte count after which an asynchronous write waits for <see cref="Resume"/> or its own
    /// cancellation token, or null to never wait.
    /// </summary>
    public long? PauseAfterBytes { get; set; }

    /// <summary>
    /// Gets the gate a paused write waits on; complete it to let writing go on.
    /// </summary>
    public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    #endregion
}
