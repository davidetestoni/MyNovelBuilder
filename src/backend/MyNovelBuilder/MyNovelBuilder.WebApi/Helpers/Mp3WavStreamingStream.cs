using System.Buffers.Binary;
using NAudio.Wave;
using NLayer.NAudioSupport;

namespace MyNovelBuilder.WebApi.Helpers;

/// <summary>Decodes incoming MP3 frames on demand, exposing PCM WAV without buffering the response.</summary>
public sealed class Mp3WavStreamingStream : Stream
{
    private readonly Stream _source;
    private readonly Action _onDispose;
    private readonly CancellationTokenSource _lifetime;
    private readonly Mp3FrameDecompressor _decoder;
    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly byte[] _floatBuffer = new byte[1152 * 2 * sizeof(float)];
    private readonly byte[] _pcmBuffer = new byte[1152 * 2 * sizeof(short)];
    private Mp3Frame? _frame;
    private ReadOnlyMemory<byte> _pending;
    private bool _disposed;

    private Mp3WavStreamingStream(Stream source, Mp3Frame frame, Action onDispose, CancellationToken token)
    {
        _source = source;
        _frame = frame;
        _onDispose = onDispose;
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
        _sampleRate = frame.SampleRate;
        _channels = frame.ChannelMode == ChannelMode.Mono ? 1 : 2;
        _decoder = new Mp3FrameDecompressor(new WaveFormat(_sampleRate, 16, _channels));
        _pending = PcmWavStreamingStream.CreateWavHeader(_sampleRate, (short)_channels, 16);
    }

    /// <summary>Reads only the first frame to determine the WAV format. Owns the source on success.</summary>
    public static async Task<Stream> CreateAsync(Stream source, Action onDispose, CancellationToken token)
    {
        var frame = await ReadFrameAsync(source, token)
                    ?? throw new InvalidDataException("OpenRouter returned no MP3 audio frames.");
        return new Mp3WavStreamingStream(source, frame, onDispose, token);
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
        var token = linked.Token;
        token.ThrowIfCancellationRequested();
        if (buffer.Length == 0) return 0;

        while (_pending.IsEmpty)
        {
            var frame = _frame ?? await ReadFrameAsync(_source, token);
            _frame = null;
            if (frame is null) return 0;
            if (frame.SampleRate != _sampleRate || (frame.ChannelMode == ChannelMode.Mono ? 1 : 2) != _channels)
                throw new InvalidDataException("MP3 audio format changed during the response.");

            var decodedBytes = _decoder.DecompressFrame(frame, _floatBuffer, 0);
            var sampleCount = decodedBytes / sizeof(float);
            for (var i = 0; i < sampleCount; i++)
            {
                var sample = BitConverter.ToSingle(_floatBuffer, i * sizeof(float));
                var pcm = (short)Math.Clamp(sample * 32767f, short.MinValue, short.MaxValue);
                BinaryPrimitives.WriteInt16LittleEndian(_pcmBuffer.AsSpan(i * sizeof(short)), pcm);
            }
            _pending = _pcmBuffer.AsMemory(0, sampleCount * sizeof(short));
        }

        var count = Math.Min(buffer.Length, _pending.Length);
        _pending[..count].CopyTo(buffer);
        _pending = _pending[count..];
        return count;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    private static async Task<Mp3Frame?> ReadFrameAsync(Stream source, CancellationToken token)
    {
        var header = new byte[4];
        var read = await source.ReadAtLeastAsync(header, 4, throwOnEndOfStream: false, cancellationToken: token);
        if (read == 0) return null;
        if (read < 4) throw new EndOfStreamException("Incomplete MP3 header.");

        // Skip ID3v2 metadata without retaining it or mistaking embedded cover art for MP3 frames.
        if (header[0] == 'I' && header[1] == 'D' && header[2] == '3')
        {
            var tagHeader = new byte[6];
            await source.ReadExactlyAsync(tagHeader, token);
            if (tagHeader.Skip(2).Any(value => (value & 0x80) != 0))
                throw new InvalidDataException("Invalid ID3 tag size.");
            var size = (tagHeader[2] << 21) | (tagHeader[3] << 14) | (tagHeader[4] << 7) | tagHeader[5];
            if (header[3] == 4 && (tagHeader[1] & 0x10) != 0) size += 10; // ID3v2.4 footer
            var discard = new byte[4096];
            while (size > 0)
            {
                var count = Math.Min(size, discard.Length);
                await source.ReadExactlyAsync(discard.AsMemory(0, count), token);
                size -= count;
            }
            return await ReadFrameAsync(source, token);
        }

        // Let NAudio parse MPEG headers, including variable bitrates. Network reads can
        // split anywhere, so assemble exactly one complete frame before decoding it.
        for (var skipped = 0; skipped < 65536; skipped++)
        {
            using var headerStream = new MemoryStream(header);
            var frame = Mp3Frame.LoadFromStream(headerStream, readData: false);
            if (frame is not null)
            {
                var data = new byte[frame.FrameLength];
                header.CopyTo(data, 0);
                await source.ReadExactlyAsync(data.AsMemory(4), token);
                using var frameStream = new MemoryStream(data);
                return Mp3Frame.LoadFromStream(frameStream);
            }
            header.AsSpan(1).CopyTo(header);
            if (await source.ReadAsync(header.AsMemory(3, 1), token) == 0) return null;
        }
        throw new InvalidDataException("No valid MP3 frame found.");
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _lifetime.Cancel();
            _source.Dispose();
            _decoder.Dispose();
            _onDispose();
            _lifetime.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    public override bool CanRead => !_disposed;
    /// <inheritdoc />
    public override bool CanSeek => false;
    /// <inheritdoc />
    public override bool CanWrite => false;
    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();
    /// <inheritdoc />
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    /// <inheritdoc />
    public override void Flush() { }
    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();
    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
