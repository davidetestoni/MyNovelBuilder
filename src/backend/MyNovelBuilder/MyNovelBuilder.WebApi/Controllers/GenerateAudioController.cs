using Microsoft.AspNetCore.Mvc;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.Prompts;
using MyNovelBuilder.WebApi.Prompts;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Services.TextGeneration;
using MyNovelBuilder.WebApi.Services.Tts;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLayer.NAudioSupport;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>
/// Controller for generating audio.
/// </summary>
[Route("api/generate/audio")]
[ApiController]
public class GenerateAudioController : ControllerBase
{
    private readonly ILogger<GenerateAudioController> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IIntegrationsService _integrationsService;

    /// <summary></summary>
    public GenerateAudioController(
        ILogger<GenerateAudioController> logger,
        IServiceProvider serviceProvider,
        IIntegrationsService integrationsService)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _integrationsService = integrationsService;
    }
    
    private async ValueTask<ITtsService> GetTtsServiceAsync(
        TtsProvider? provider = null,
        CancellationToken cancellationToken = default)
    {
        TtsProvider ttsProvider;
        
        if (provider.HasValue)
        {
            ttsProvider = provider.Value;
        }
        else
        {
            var config = await _integrationsService.GetConfigAsync(cancellationToken);
            ttsProvider = config.TtsProvider;
        }
        
        var ttsService = _serviceProvider.GetKeyedService<ITtsService>(ttsProvider);

        if (ttsService is null)
        {
            _logger.LogError(
                "Unsupported TTS provider: {Provider}", ttsProvider);

            throw new InvalidOperationException(
                $"Unsupported TTS provider: {ttsProvider}");
        }

        return ttsService;
    }
    
    private async ValueTask<ITextGenerationService> GetTextGenerationServiceAsync(
        CancellationToken cancellationToken = default)
    {
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        var textGenerationService = _serviceProvider.GetKeyedService<ITextGenerationService>(config.TextGenerationProvider);

        if (textGenerationService is null)
        {
            _logger.LogError(
                "Unsupported text generation provider: {Provider}", config.TextGenerationProvider);
            
            throw new InvalidOperationException(
                $"Unsupported text generation provider: {config.TextGenerationProvider}");
        }

        return textGenerationService;
    }
    
    private async Task<string> GetEmphasizedTextAsync(
        string inputText,
        CancellationToken cancellationToken = default)
    {
        var textGenerationService = await GetTextGenerationServiceAsync(cancellationToken);
        return await textGenerationService
            .GenerateAsync(
                "anthropic/claude-sonnet-4", // TODO: Make configurable
                [
                    new PromptMessage
                    {
                        Role = PromptMessageRole.System,
                        Message = SystemPrompts.EmphasizeText
                    },
                    new PromptMessage
                    {
                        Role = PromptMessageRole.User,
                        Message = $"Here's the text that needs to be enriched:\n{inputText}"
                    }
                ],
                cancellationToken: cancellationToken
            );
    }
    
    private static async Task<Stream> ConvertMp3ToWavStreamAsync(
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

    /// <summary>
    /// Generate audio from text.
    /// </summary>
    [HttpPost("tts")]
    public async Task<ActionResult> GenerateAudioAsync(
        TtsRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var ttsService = await GetTtsServiceAsync(cancellationToken: cancellationToken);
        
        _logger.LogInformation("Generating audio for text: {Text}", dto.Message);
        
        // Emphasis
        if (ttsService.SupportsEmphasisTags)
        {
            var emphasizedText = await GetEmphasizedTextAsync(dto.Message, cancellationToken);
            dto.Message = emphasizedText.Trim();
            
            _logger.LogInformation("Emphasized text: {EmphasizedText}", dto.Message);
        }
        
        var ttsResponse = await ttsService.GenerateAudioAsync(dto, cancellationToken);
        var mimeType = ttsService.OutputAudioFormat switch 
        {
            AudioFormat.Mp3 => "audio/mp3",
            AudioFormat.Wav => "audio/wav",
            _ => "application/octet-stream"
        };
        var fileName = ttsService.OutputAudioFormat switch 
        {
            AudioFormat.Mp3 => "audio.mp3",
            AudioFormat.Wav => "audio.wav",
            _ => "audio.bin"
        };
        
        return File(ttsResponse, mimeType, fileName);
    }

    /// <summary>
    /// Generate audio stream from text.
    /// </summary>
    [HttpPost("tts/stream")]
    public async Task<ActionResult> GenerateAudioStreamAsync(
        TtsRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var ttsService = await GetTtsServiceAsync(cancellationToken: cancellationToken);
        
        _logger.LogInformation("Generating audio stream for text: {Text}", dto.Message);
        
        // Emphasis
        if (ttsService.SupportsEmphasisTags)
        {
            var emphasizedText = await GetEmphasizedTextAsync(dto.Message, cancellationToken);
            dto.Message = emphasizedText.Trim();
            
            _logger.LogInformation("Emphasized text: {EmphasizedText}", dto.Message);
        }
        
        Stream audioStream;

        try
        {
            audioStream = await ttsService.GenerateAudioStreamAsync(dto, cancellationToken);
        }
        catch (NotImplementedException)
        {
            // Fallback to non-streaming
            var audioBytes = await ttsService.GenerateAudioAsync(dto, cancellationToken);
            audioStream = new MemoryStream(audioBytes);
        }

        if (ttsService.OutputAudioFormat == AudioFormat.Mp3)
        {
            var originalStream = audioStream;
            await using (originalStream)
            {
                audioStream = await ConvertMp3ToWavStreamAsync(
                    originalStream,
                    cancellationToken);
            }
        }
        
        return File(audioStream, "audio/wav", "audio.wav");
    }

    /// <summary>
    /// Get available TTS voices.
    /// </summary>
    [HttpGet("tts/voices")]
    public async Task<ActionResult<IEnumerable<TtsVoiceDto>>> GetVoices(
        [FromQuery] TtsProvider? provider,
        CancellationToken cancellationToken = default)
    {
        var ttsService = await GetTtsServiceAsync(provider, cancellationToken);
        
        var voices = await ttsService.GetVoicesAsync(cancellationToken);
        
        return Ok(voices);
    }
}
