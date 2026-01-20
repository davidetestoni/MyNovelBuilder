using KokoroSharp;
using KokoroSharp.Utilities;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using NAudio.Wave;

namespace MyNovelBuilder.WebApi.Services.Tts;

/// <summary>
/// Text-to-Speech service for the local Kokoro model.
/// </summary>
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
    public KokoroTtsService(ILogger<KokoroTtsService> logger)
    {
        _logger = logger;
    }
    
    /// <inheritdoc />
    public async Task<byte[]> GenerateAudioAsync(TtsRequestDto request)
    {
        _logger.LogInformation("Generating audio using Kokoro TTS");
        
        // Download the model if not present
        await KokoroTTS.LoadModelAsync(model: KModel.float32);
        
        var synth = new KokoroWavSynthesizer("kokoro.onnx");
        var voice = KokoroVoiceManager.GetVoice(request.VoiceId);

        var audioBytes = await synth.SynthesizeAsync(request.Message, voice);
        
        // The bytes aren't in wav format, so we need to encode them
        using var ms = new MemoryStream();
        await using var writer = new WaveFileWriter(ms, KokoroPlayback.waveFormat);
        await writer.WriteAsync(audioBytes);

        ms.Seek(0, SeekOrigin.Begin);

        return ms.ToArray();
    }
    
    /// <inheritdoc />
    public Task<Stream> GenerateAudioStreamAsync(TtsRequestDto request)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<IEnumerable<TtsVoiceDto>> GetVoicesAsync()
    {
        return Task.FromResult(_voices.Select(v => new TtsVoiceDto
        {
            VoiceId = v,
            Name = v,
        }));
    }
}
