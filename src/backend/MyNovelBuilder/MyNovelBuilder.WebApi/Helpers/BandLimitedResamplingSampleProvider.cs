using NAudio.Dsp;
using NAudio.Wave;

namespace MyNovelBuilder.WebApi.Helpers;

/// <summary>Resamples with a sinc low-pass filter to suppress aliasing in speech transients.</summary>
public sealed class BandLimitedResamplingSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly WdlResampler _resampler = new();

    /// <summary>Creates a bounded-buffer resampler with a 128-tap sinc filter.</summary>
    public BandLimitedResamplingSampleProvider(ISampleProvider source, int sampleRate)
    {
        _source = source;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, source.WaveFormat.Channels);
        _resampler.SetMode(false, 0, true, 128);
        _resampler.SetFeedMode(false);
        _resampler.SetRates(source.WaveFormat.SampleRate, sampleRate);
    }

    /// <inheritdoc />
    public WaveFormat WaveFormat { get; }

    /// <inheritdoc />
    public int Read(float[] buffer, int offset, int count)
    {
        var channels = WaveFormat.Channels;
        var frames = count / channels;
        if (frames == 0) return 0;
        var needed = _resampler.ResamplePrepare(frames, channels, out var input, out var inputOffset);
        var available = _source.Read(input, inputOffset, needed * channels) / channels;
        return _resampler.ResampleOut(buffer, offset, available, frames, channels) * channels;
    }
}
