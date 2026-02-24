using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MyNovelBuilder.WebApi.Helpers;

/// <summary>
/// Wraps a readable stream and buffers bytes as they are read, then persists
/// the full payload only if the stream is fully consumed without cancellation.
/// </summary>
internal sealed class CachingReadStream : Stream
{
    private readonly Stream _source;
    private readonly MemoryStream _buffer = new();
    private readonly Func<byte[], CancellationToken, Task> _saveAsync;
    private readonly CancellationToken _cancellationToken;
    private bool _fullyRead;
    private bool _saveAttempted;

    /// <summary>
    /// Initializes a new caching wrapper stream.
    /// </summary>
    public CachingReadStream(
        Stream source,
        Func<byte[], CancellationToken, Task> saveAsync,
        CancellationToken cancellationToken)
    {
        _source = source;
        _saveAsync = saveAsync;
        _cancellationToken = cancellationToken;
    }

    private async Task TrySaveAsync()
    {
        if (_saveAttempted || !_fullyRead || _cancellationToken.IsCancellationRequested)
        {
            return;
        }

        _saveAttempted = true;
        await _saveAsync(_buffer.ToArray(), _cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = _source.Read(buffer, offset, count);
        if (bytesRead > 0)
        {
            _buffer.Write(buffer, offset, bytesRead);
        }
        else
        {
            _fullyRead = true;
        }

        return bytesRead;
    }

    public override int Read(Span<byte> buffer)
    {
        var bytesRead = _source.Read(buffer);
        if (bytesRead > 0)
        {
            _buffer.Write(buffer[..bytesRead]);
        }
        else
        {
            _fullyRead = true;
        }

        return bytesRead;
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        var bytesRead = await _source.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        if (bytesRead > 0)
        {
            await _buffer.WriteAsync(buffer.AsMemory(offset, bytesRead), cancellationToken);
        }
        else
        {
            _fullyRead = true;
        }

        return bytesRead;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var bytesRead = await _source.ReadAsync(buffer, cancellationToken);
        if (bytesRead > 0)
        {
            await _buffer.WriteAsync(buffer[..bytesRead], cancellationToken);
        }
        else
        {
            _fullyRead = true;
        }

        return bytesRead;
    }

    public override bool CanRead => _source.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            TrySaveAsync().GetAwaiter().GetResult();
            _source.Dispose();
            _buffer.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await TrySaveAsync();
        await _source.DisposeAsync();
        await _buffer.DisposeAsync();
        await base.DisposeAsync();
    }
}
