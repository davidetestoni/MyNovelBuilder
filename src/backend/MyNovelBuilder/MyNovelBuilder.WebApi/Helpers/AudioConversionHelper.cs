using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLayer.NAudioSupport;

namespace MyNovelBuilder.WebApi.Helpers;

/// <summary>
/// Utilities for converting audio between formats used by external providers.
/// </summary>
public static class AudioConversionHelper
{
    /// <summary>
    /// Detects whether the provided bytes look like an MP3 stream.
    /// </summary>
    public static bool LooksLikeMp3(byte[] audioBytes)
    {
        if (audioBytes.Length < 2)
        {
            return false;
        }

        if (audioBytes.Length >= 3
            && audioBytes[0] == (byte)'I'
            && audioBytes[1] == (byte)'D'
            && audioBytes[2] == (byte)'3')
        {
            return true;
        }

        return audioBytes[0] == 0xFF && (audioBytes[1] & 0xE0) == 0xE0;
    }

    /// <summary>
    /// Determines whether the provided content type represents MP3 audio.
    /// </summary>
    public static bool IsMp3ContentType(string? contentType)
    {
        return string.Equals(contentType, "audio/mpeg", StringComparison.OrdinalIgnoreCase)
               || string.Equals(contentType, "audio/mp3", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Converts MP3 audio bytes into WAV audio bytes.
    /// </summary>
    public static async Task<byte[]> ConvertMp3ToWavBytesAsync(
        byte[] mp3Bytes,
        CancellationToken cancellationToken = default)
    {
        await using var mp3Stream = new MemoryStream(mp3Bytes);
        await using var wavStream = await ConvertMp3ToWavStreamAsync(mp3Stream, cancellationToken);
        await using var wavBuffer = new MemoryStream();
        await wavStream.CopyToAsync(wavBuffer, cancellationToken);
        return wavBuffer.ToArray();
    }

    /// <summary>
    /// Converts an MP3 stream into a WAV stream.
    /// </summary>
    public static async Task<Stream> ConvertMp3ToWavStreamAsync(
        Stream mp3Stream,
        CancellationToken cancellationToken)
    {
        // TODO: Make this not blocking by streaming the conversion
        //  instead of buffering the entire MP3 in memory first.
        Stream? bufferedMp3 = null;
        var sourceStream = mp3Stream;

        if (!mp3Stream.CanSeek)
        {
            bufferedMp3 = new MemoryStream();
            await mp3Stream.CopyToAsync(bufferedMp3, cancellationToken);
            bufferedMp3.Position = 0;
            sourceStream = bufferedMp3;
        }
        else
        {
            mp3Stream.Position = 0;
        }

        try
        {
            var builder = new Mp3FileReaderBase.FrameDecompressorBuilder(wf => new Mp3FrameDecompressor(wf));
            await using var reader = new Mp3FileReaderBase(sourceStream, builder);
            var sampleProvider = reader.ToSampleProvider();
            var pcm16Provider = new SampleToWaveProvider16(sampleProvider);
            using var wavBuffer = new MemoryStream();
            WaveFileWriter.WriteWavFileToStream(wavBuffer, pcm16Provider);
            return new MemoryStream(wavBuffer.ToArray());
        }
        finally
        {
            if (bufferedMp3 is not null)
            {
                await bufferedMp3.DisposeAsync();
            }
        }
    }
}
