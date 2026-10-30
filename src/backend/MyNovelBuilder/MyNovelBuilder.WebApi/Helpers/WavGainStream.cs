using System.Buffers.Binary;

namespace MyNovelBuilder.WebApi.Helpers;

/// <summary>Applies fixed gain to WAV samples incrementally, preserving headers and raw source ownership.</summary>
public sealed class WavGainStream(Stream source, double gainDb) : Stream
{
    private readonly double _gain = double.IsFinite(gainDb) ? Math.Pow(10, gainDb / 20) : throw new ArgumentOutOfRangeException(nameof(gainDb));
    private bool _initialized;
    private long _remaining;
    private int _sampleBytes;
    private int _format;
    private byte[] _pending = [];
    private int _offset;
    private bool _data;

    /// <summary>Applies fixed gain to a WAV buffer and returns the resulting audio.</summary>
    public static async Task<byte[]> ApplyAsync(byte[] wav, double gainDb, CancellationToken ct)
    {
        if (gainDb == 0) return wav;
        await using var stream = new WavGainStream(new MemoryStream(wav, false), gainDb);
        using var output = new MemoryStream();
        await stream.CopyToAsync(output, ct);
        return output.ToArray();
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.Length == 0) return 0;
        if (_offset == _pending.Length)
        {
            _offset = 0;
            if (!_initialized)
            {
                _pending = new byte[12];
                await source.ReadExactlyAsync(_pending, cancellationToken);
                if (!_pending.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !_pending.AsSpan(8, 4).SequenceEqual("WAVE"u8))
                    throw new InvalidDataException("Expected a RIFF WAV stream.");
                _initialized = true;
            }
            else if (!_data)
            {
                var header = new byte[8];
                var count = await source.ReadAsync(header.AsMemory(0, 1), cancellationToken);
                if (count == 0) return 0;
                await source.ReadExactlyAsync(header.AsMemory(1), cancellationToken);
                var size = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4));
                if (header.AsSpan(0, 4).SequenceEqual("data"u8))
                {
                    if (_sampleBytes == 0) throw new InvalidDataException("WAV format must precede audio data.");
                    _remaining = size == uint.MaxValue || size == 0 ? long.MaxValue : size;
                    _data = true;
                    _pending = header;
                }
                else
                {
                    // WAV metadata chunks are passed through exactly, including padding.
                    if (size > 16 * 1024 * 1024) throw new InvalidDataException("WAV metadata chunk is too large.");
                    _pending = new byte[8 + size + (size & 1)];
                    header.CopyTo(_pending, 0);
                    await source.ReadExactlyAsync(_pending.AsMemory(8), cancellationToken);
                    if (header.AsSpan(0, 4).SequenceEqual("fmt "u8))
                    {
                        if (size < 16) throw new InvalidDataException("Invalid WAV format.");
                        _format = BinaryPrimitives.ReadUInt16LittleEndian(_pending.AsSpan(8));
                        _sampleBytes = BinaryPrimitives.ReadUInt16LittleEndian(_pending.AsSpan(22)) / 8;
                        if (_format == 65534 && size >= 40)
                            _format = BinaryPrimitives.ReadUInt16LittleEndian(_pending.AsSpan(32));
                        if (!(_format == 1 && _sampleBytes is >= 1 and <= 4 || _format == 3 && _sampleBytes == 4))
                            throw new NotSupportedException("Gain requires PCM or 32-bit float WAV audio.");
                    }
                }
            }
            else
            {
                if (_remaining == 0) return await source.ReadAsync(buffer, cancellationToken);
                var data = new byte[(int)Math.Min(8192 - 8192 % _sampleBytes, _remaining)];
                var count = await source.ReadAsync(data, cancellationToken);
                if (count == 0) return 0;
                var extra = (_sampleBytes - count % _sampleBytes) % _sampleBytes;
                if (count + extra > data.Length) throw new InvalidDataException("Incomplete WAV sample.");
                if (extra > 0) await source.ReadExactlyAsync(data.AsMemory(count, extra), cancellationToken);
                count += extra;
                _remaining -= count;
                for (var i = 0; i < count; i += _sampleBytes) Scale(data.AsSpan(i, _sampleBytes));
                _pending = data[..count];
            }
        }
        var length = Math.Min(buffer.Length, _pending.Length - _offset);
        _pending.AsMemory(_offset, length).CopyTo(buffer);
        _offset += length;
        return length;
    }

    private void Scale(Span<byte> sample)
    {
        if (_format == 3)
        {
            BinaryPrimitives.WriteSingleLittleEndian(sample, (float)(BinaryPrimitives.ReadSingleLittleEndian(sample) * _gain));
            return;
        }
        long value = _sampleBytes switch
        {
            1 => sample[0] - 128,
            2 => BinaryPrimitives.ReadInt16LittleEndian(sample),
            3 => (sample[0] | sample[1] << 8 | sample[2] << 16) << 8 >> 8,
            _ => BinaryPrimitives.ReadInt32LittleEndian(sample)
        };
        var limit = 1L << (_sampleBytes * 8 - 1);
        // Saturate at the integer format bounds; no limiter or automatic leveling.
        value = (long)Math.Clamp(Math.Round(value * _gain), -limit, limit - 1);
        if (_sampleBytes == 1) sample[0] = (byte)(value + 128);
        else for (var i = 0; i < _sampleBytes; i++) sample[i] = (byte)(value >> (i * 8));
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) source.Dispose();
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await source.DisposeAsync();
        GC.SuppressFinalize(this);
    }
    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
    /// <inheritdoc />
    public override bool CanRead => true;
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
