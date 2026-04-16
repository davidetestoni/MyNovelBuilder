using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
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
    private ExceptionDispatchInfo? _producerException;
    private bool _headerWritten;
    private bool _isComplete;
    private byte[] _currentChunk = [];
    private int _currentChunkPosition;
    private readonly int _sampleRate;
    private readonly short _channels;
    private readonly short _bitsPerSample;

    /// <summary>
    /// Initializes a stream that prepends a WAV header and then streams PCM chunks from an async producer.
    /// </summary>
    /// <param name="sampleRate">PCM sample rate in Hz.</param>
    /// <param name="channels">Number of channels (for example, 1 for mono or 2 for stereo).</param>
    /// <param name="bitsPerSample">Bits per sample (for example, 16).</param>
    /// <param name="producer">Async producer callback that pushes PCM chunks into this stream.</param>
    /// <param name="ct">Cancellation token used by the producer loop.</param>
    /// <param name="onDispose">Optional callback invoked when the stream is disposed.</param>
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
        catch (Exception ex)
        {
            _producerException = ExceptionDispatchInfo.Capture(ex);
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

    /// <summary>
    /// Reads bytes from the stream, blocking until data is available or the stream completes.
    /// </summary>
    /// <param name="buffer">Destination buffer.</param>
    /// <param name="offset">Offset in <paramref name="buffer"/> where data is written.</param>
    /// <param name="count">Maximum number of bytes to read.</param>
    /// <returns>The number of bytes read.</returns>
    public override int Read(byte[] buffer, int offset, int count)
        => ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Asynchronously reads bytes from the stream, first emitting the WAV header and then PCM data.
    /// </summary>
    /// <param name="buffer">Destination buffer.</param>
    /// <param name="offset">Offset in <paramref name="buffer"/> where data is written.</param>
    /// <param name="count">Maximum number of bytes to read.</param>
    /// <param name="cancellationToken">Cancellation token for waiting on incoming data.</param>
    /// <returns>A task that resolves to the number of bytes read.</returns>
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (!_headerWritten && _isComplete && _producerException is not null)
        {
            _producerException.Throw();
        }

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
                    if (totalRead == 0 && _producerException is not null)
                    {
                        _producerException.Throw();
                    }
                    break;
                }

                await _semaphore.WaitAsync(cancellationToken);

                if (_isComplete && _chunks.Count == 0)
                {
                    if (totalRead == 0 && _producerException is not null)
                    {
                        _producerException.Throw();
                    }
                    break;
                }
            }
        }

        return totalRead;
    }

    /// <summary>
    /// Creates a RIFF/WAVE header configured for PCM data with an unspecified (max-size) data length.
    /// </summary>
    /// <param name="sampleRate">PCM sample rate in Hz.</param>
    /// <param name="channels">Number of channels.</param>
    /// <param name="bitsPerSample">Bits per sample.</param>
    /// <returns>The WAV header bytes.</returns>
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

    /// <summary>
    /// Gets a value indicating whether the stream supports reading.
    /// </summary>
    public override bool CanRead => true;

    /// <summary>
    /// Gets a value indicating whether the stream supports seeking.
    /// </summary>
    public override bool CanSeek => false;

    /// <summary>
    /// Gets a value indicating whether the stream supports writing.
    /// </summary>
    public override bool CanWrite => false;

    /// <summary>
    /// Gets the length of the stream. This operation is not supported.
    /// </summary>
    public override long Length => throw new NotSupportedException();

    /// <summary>
    /// Gets or sets the current position in the stream. This operation is not supported.
    /// </summary>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Flush is a no-op because this stream is read-only.
    /// </summary>
    public override void Flush() { }

    /// <summary>
    /// Seeks to a position in the stream. This operation is not supported.
    /// </summary>
    /// <param name="offset">Byte offset relative to <paramref name="origin"/>.</param>
    /// <param name="origin">Reference point for <paramref name="offset"/>.</param>
    /// <returns>This method always throws.</returns>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <summary>
    /// Sets stream length. This operation is not supported.
    /// </summary>
    /// <param name="value">The desired length.</param>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <summary>
    /// Writes to the stream. This operation is not supported.
    /// </summary>
    /// <param name="buffer">Source buffer.</param>
    /// <param name="offset">Offset in <paramref name="buffer"/> to start reading from.</param>
    /// <param name="count">Number of bytes to write.</param>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>
    /// Releases stream resources and invokes the optional dispose callback.
    /// </summary>
    /// <param name="disposing">Whether managed resources should be disposed.</param>
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
