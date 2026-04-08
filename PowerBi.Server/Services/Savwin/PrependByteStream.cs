namespace PowerBi.Server.Services.Savwin;

/// <summary>
/// Reinsere um byte já lido no início de um <see cref="Stream"/> (para inspecionar o primeiro token JSON sem perder o restante).
/// </summary>
internal sealed class PrependByteStream : Stream
{
    private readonly byte _first;
    private readonly Stream _inner;
    private bool _firstByteReturned;

    public PrependByteStream(byte first, Stream inner)
    {
        _first = first;
        _inner = inner;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (!_firstByteReturned && count > 0)
        {
            _firstByteReturned = true;
            buffer[offset] = _first;
            return 1;
        }

        return _inner.Read(buffer, offset, count);
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        if (!_firstByteReturned && buffer.Length > 0)
        {
            _firstByteReturned = true;
            buffer.Span[0] = _first;
            return 1;
        }

        return await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
