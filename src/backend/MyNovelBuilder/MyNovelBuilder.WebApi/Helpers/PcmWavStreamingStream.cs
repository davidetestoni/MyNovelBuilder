using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MyNovelBuilder.WebApi.Helpers;

/// <summary>
/// Stream that consumes PCM chunks from an async producer and exposes a WAV stream.
/// </summary>
public sealed class PcmWavStreamingStream : Stream
{
    private readonly Func<Func<ReadOnlyMemory<byte>, Task>, CancellationToken, Task> _producer;
    private readonly CancellationToken _ct;
    private readonly Action? _onDispose;
    private readonly Queue<byte[]> _chunks = new();
    private readonly SemaphoreSlim _semaphore = new(0);
    private bool _headerWritten;
    private bool _isComplete;
    private byte[] _currentChunk = [];
    private int _currentChunkPosition;
    private readonly int _sampleRate;
    private readonly short _channels;
    private readonly short _bitsPerSample;

    public PcmWavStreamingStream(
        int sampleRate,
        short channels,
        short bitsPerSample,
        Func<Func<ReadOnlyMemory<byte>, Task>, CancellationToken, Task> producer,
        CancellationToken ct,
        Action? onDispose = null)
    {
        _sampleRate = sampleRate;
        _channels = channels;
        _bitsPerSample = bitsPerSample;
        _producer = producer;
        _ct = ct;
        _onDispose = onDispose;

        _ = Task.Run(ProduceAsync, ct);
    }

    private async Task ProduceAsync()
    {
        try
        {
            await _producer(EnqueueAsync, _ct);
        }
        finally
        {
            _isComplete = true;
            _semaphore.Release();
        }
    }

    private Task EnqueueAsync(ReadOnlyMemory<byte> data)
    {
        if (data.Length == 0)
        {
            return Task.CompletedTask;
        }

        var buffer = data.ToArray();
        lock (_chunks)
        {
            _chunks.Enqueue(buffer);
        }
        _semaphore.Release();
        return Task.CompletedTask;
    }

    public override int Read(byte[] buffer, int offset, int count)
        => ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (!_headerWritten)
        {
            _headerWritten = true;
            var header = CreateWavHeader(_sampleRate, _channels, _bitsPerSample);
            var bytesToCopy = Math.Min(count, header.Length);
            Array.Copy(header, 0, buffer, offset, bytesToCopy);

            if (bytesToCopy < header.Length)
            {
                _currentChunk = header;
                _currentChunkPosition = bytesToCopy;
            }

            return bytesToCopy;
        }

        var totalRead = 0;

        while (totalRead < count && _currentChunkPosition < _currentChunk.Length)
        {
            var bytesToCopy = Math.Min(count - totalRead, _currentChunk.Length - _currentChunkPosition);
            Array.Copy(_currentChunk, _currentChunkPosition, buffer, offset + totalRead, bytesToCopy);
            _currentChunkPosition += bytesToCopy;
            totalRead += bytesToCopy;
        }

        while (totalRead < count)
        {
            byte[]? nextChunk = null;

            lock (_chunks)
            {
                if (_chunks.Count > 0)
                {
                    nextChunk = _chunks.Dequeue();
                }
            }

            if (nextChunk != null)
            {
                _currentChunk = nextChunk;
                _currentChunkPosition = 0;

                var bytesToCopy = Math.Min(count - totalRead, _currentChunk.Length);
                Array.Copy(_currentChunk, 0, buffer, offset + totalRead, bytesToCopy);
                _currentChunkPosition += bytesToCopy;
                totalRead += bytesToCopy;
            }
            else
            {
                if (_isComplete)
                {
                    break;
                }

                await _semaphore.WaitAsync(cancellationToken);

                if (_isComplete && _chunks.Count == 0)
                {
                    break;
                }
            }
        }

        return totalRead;
    }

    public static byte[] CreateWavHeader(int sampleRate, short channels, short bitsPerSample)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        const int maxSize = int.MaxValue - 44;
        var bytesPerSample = bitsPerSample / 8;
        var byteRate = sampleRate * channels * bytesPerSample;
        var blockAlign = (short)(channels * bytesPerSample);

        writer.Write(new[] { 'R', 'I', 'F', 'F' });
        writer.Write(36 + maxSize);
        writer.Write(new[] { 'W', 'A', 'V', 'E' });

        writer.Write(new[] { 'f', 'm', 't', ' ' });
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);

        writer.Write(new[] { 'd', 'a', 't', 'a' });
        writer.Write(maxSize);

        return ms.ToArray();
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
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _onDispose?.Invoke();
            _semaphore.Dispose();
        }
        base.Dispose(disposing);
    }
}
