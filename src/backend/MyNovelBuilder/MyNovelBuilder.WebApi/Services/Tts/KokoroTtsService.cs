using KokoroSharp;
using KokoroSharp.Core;
using KokoroSharp.Processing;
using KokoroSharp.Utilities;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.Tts;
using NAudio.Wave;

using MyNovelBuilder.WebApi.Attributes;

namespace MyNovelBuilder.WebApi.Services.Tts;

/// <summary>
/// Text-to-Speech service for the local Kokoro model.
/// </summary>
[RegisterKeyedService(TtsProvider.Kokoro)]
public class KokoroTtsService : ITtsService
{
    private readonly ILogger<KokoroTtsService> _logger;

    private readonly string[] _voices =
    [
        "af_heart",
        "af_alloy",
        "af_aoede",
        "af_bella",
        "af_heart",
        "af_heart",
        "af_jessica",
        "af_kore",
        "af_nicole",
        "af_nova",
        "af_river",
        "af_sarah",
        "af_sky",
        "am_adam",
        "am_echo",
        "am_eric",
        "am_fenrir",
        "am_liam",
        "am_michael",
        "am_onyx",
        "am_puck",
        "am_santa",
        "am_santa",
        "bf_alice",
        "bf_emma",
        "bf_isabella",
        "bf_lily",
        "bm_daniel",
        "bm_fable",
        "bm_george",
        "bm_lewis",
        "ef_dora",
        "em_alex",
        "em_santa",
        "ff_siwis",
        "ff_siwis",
        "hf_alpha",
        "hf_beta",
        "hm_omega",
        "hm_psi",
        "if_sara",
        "im_nicola",
        "jf_alpha",
        "jf_gongitsune",
        "jf_nezumi",
        "jf_tebukuro",
        "jm_kumo",
        "pf_dora",
        "pm_alex",
        "pm_santa",
        "zf_xiaobei",
        "zf_xiaoni",
        "zf_xiaoxiao",
        "zf_xiaoyi",
        "zm_yunjian",
        "zm_yunxi",
        "zm_yunxia",
        "zm_yunyang",
    ];
    
    /// <inheritdoc />
    public bool SupportsEmphasisTags => false;

    /// <inheritdoc />
    public AudioFormat OutputAudioFormat => AudioFormat.Wav;
    
    /// <summary></summary>
    public KokoroTtsService(
        ILogger<KokoroTtsService> logger)
    {
        _logger = logger;
    }
    
    /// <inheritdoc />
    public async Task<byte[]> GenerateAudioAsync(
        TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating audio using Kokoro TTS");
        var normalizedMessage = NormalizeForKokoro(request.Message);
        
        // Download the model if not present
        cancellationToken.ThrowIfCancellationRequested();
        await KokoroTTS.LoadModelAsync(model: KModel.float32);
        
        var synth = new KokoroWavSynthesizer("kokoro.onnx");
        var voice = KokoroVoiceManager.GetVoice(request.VoiceId);

        cancellationToken.ThrowIfCancellationRequested();
        var audioBytes = await synth.SynthesizeAsync(normalizedMessage, voice);
        
        // The bytes aren't in wav format, so we need to encode them
        using var ms = new MemoryStream();
        await using var writer = new WaveFileWriter(ms, KokoroPlayback.waveFormat);
        await writer.WriteAsync(audioBytes, cancellationToken);

        ms.Seek(0, SeekOrigin.Begin);

        return ms.ToArray();
    }
    
    /// <inheritdoc />
    public Task<Stream> GenerateAudioStreamAsync(
        TtsRequest request,
        CancellationToken cancellationToken = default) => GenerateAudioStreamInternalAsync(request, cancellationToken);
    
    private async Task<Stream> GenerateAudioStreamInternalAsync(
        TtsRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating streaming audio using Kokoro TTS");
        var normalizedMessage = NormalizeForKokoro(request.Message);

        // Download model if missing.
        cancellationToken.ThrowIfCancellationRequested();
        await KokoroTTS.LoadModelAsync(model: KModel.float32);

        var voice = KokoroVoiceManager.GetVoice(request.VoiceId);

        return new PcmWavStreamingStream(
            sampleRate: KokoroPlayback.waveFormat.SampleRate,
            channels: (short)KokoroPlayback.waveFormat.Channels,
            bitsPerSample: (short)KokoroPlayback.waveFormat.BitsPerSample,
            producer: async (writeAsync, ct) =>
            {
                using var tts = KokoroTTS.LoadModel("kokoro.onnx");

                var tokens = Tokenizer.Tokenize(normalizedMessage);
                var segments = SegmentationSystem.SplitToSegments(tokens, new DefaultSegmentationConfig());

                Exception? callbackException = null;
                var job = KokoroJob.Create(segments, voice, 1f, samples =>
                {
                    if (samples.Length == 0 || callbackException is not null || ct.IsCancellationRequested)
                    {
                        return;
                    }

                    try
                    {
                        var pcmBytes = ToPcm16Bytes(samples);
                        writeAsync(pcmBytes).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        callbackException = ex;
                    }
                });

                tts.EnqueueJob(job);

                try
                {
                    while (!job.isDone && !ct.IsCancellationRequested)
                    {
                        if (callbackException is not null)
                        {
                            throw callbackException;
                        }

                        await Task.Delay(10, ct);
                    }
                }
                catch (OperationCanceledException)
                {
                    job.Cancel();
                    throw;
                }

                if (callbackException is not null)
                {
                    throw callbackException;
                }
            },
            ct: cancellationToken);
    }

    private static string NormalizeForKokoro(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        return input
            // Quotes/apostrophes
            .Replace('\u2018', '\'')
            .Replace('\u2019', '\'')
            .Replace('\u201B', '\'')
            .Replace('\u2032', '\'')
            .Replace('\u2035', '\'')
            .Replace('\u201C', '"')
            .Replace('\u201D', '"')
            .Replace('\u201F', '"')
            .Replace('\u2033', '"')
            .Replace('\u2036', '"')
            // Dashes and ellipsis
            .Replace('\u2013', ',')
            .Replace('\u2014', ',')
            .Replace('\u2015', ',')
            .Replace("\u2026", "...")
            // Spacing / separators
            .Replace('\u00A0', ' ')
            .Replace('\u202F', ' ')
            .Replace('\u2007', ' ')
            .Replace('\u2028', '\n')
            .Replace('\u2029', '\n');
    }

    private static byte[] ToPcm16Bytes(float[] samples)
    {
        var output = new byte[samples.Length * sizeof(short)];

        for (var i = 0; i < samples.Length; i++)
        {
            var clamped = Math.Clamp(samples[i], -1f, 1f);
            var sample = (short)Math.Round(clamped * short.MaxValue);
            var offset = i * sizeof(short);
            output[offset] = (byte)(sample & 0xFF);
            output[offset + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return output;
    }

    /// <inheritdoc />
    public Task<IEnumerable<TtsVoiceDto>> GetVoicesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_voices.Select(v => new TtsVoiceDto
        {
            VoiceId = v,
            Name = v,
            Language = WritingLanguage.English
        }));
    }
}
