﻿using System.Buffers;
using System.IO.Compression;

namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// Compresses the streams the library writes itself as zlib data for <c>/Filter /FlateDecode</c> (WR-042), with the
/// platform's own <see cref="ZLibStream"/> at <see cref="CompressionLevel.Optimal"/>. It keeps no state between calls,
/// so threads may share it, and the same input on the same runtime always gives the same bytes.
/// </summary>
internal static class FlateEncoder
{
    #region Fields and Constants

    /// <summary>
    /// The PDF filter name of the data <see cref="Compress"/> returns.
    /// </summary>
    public const string FilterName = "/FlateDecode";

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns <paramref name="data"/> compressed as zlib data. Empty data gives the 8-byte zlib stream that unpacks to
    /// nothing on every runtime: some runtimes' compressors write no bytes at all for it, which is not a valid stream.
    /// The compressor writes into a pooled buffer, so the only new array is the returned one, of the exact size.
    /// </summary>
    /// <param name="data">The stream's unpacked bytes.</param>
    /// <returns>The compressed bytes.</returns>
    public static byte[] Compress(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return EmptyStream.ToArray();

        using var output = new PooledOutput(data.Length / 2 + 64);
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(data);
        }
        return output.ToArray();
    }

    #endregion

    #endregion

    #region Properties, Indexers

    // A zlib header for the default level, one empty final fixed-Huffman block, and the Adler-32 of no data: the bytes
    // the platform compressor writes for empty data where it writes any.
    private static ReadOnlySpan<byte> EmptyStream => new byte[] { 0x78, 0x9C, 0x03, 0x00, 0x00, 0x00, 0x00, 0x01 };

    #endregion

    #region Nested Types

    // A write-only stream over a buffer rented from the shared pool, grown by renting a larger one. Disposing returns
    // the buffer.
    private sealed class PooledOutput : Stream
    {
        #region Fields and Constants

        private byte[] _buffer;
        private int _length;

        #endregion

        #region Constructors and Finalizers

        public PooledOutput(int capacity) => _buffer = ArrayPool<byte>.Shared.Rent(capacity);

        #endregion

        #region Methods

        #region Protected Override Methods

        protected override void Dispose(bool disposing)
        {
            if (disposing && _buffer.Length > 0)
            {
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = Array.Empty<byte>();
            }
            base.Dispose(disposing);
        }

        #endregion

        #region Public Methods

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public byte[] ToArray() => _buffer.AsSpan(0, _length).ToArray();

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (_buffer.Length - _length < buffer.Length)
            {
                var larger = ArrayPool<byte>.Shared.Rent(Math.Max(_buffer.Length * 2, _length + buffer.Length));
                _buffer.AsSpan(0, _length).CopyTo(larger);
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = larger;
            }
            buffer.CopyTo(_buffer.AsSpan(_length));
            _length += buffer.Length;
        }

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        #endregion

        #endregion

        #region Properties, Indexers

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => _length;

        public override long Position
        {
            get => _length;
            set => throw new NotSupportedException();
        }

        #endregion
    }

    #endregion
}
