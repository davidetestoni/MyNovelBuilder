using Microsoft.AspNetCore.Mvc;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Dtos.Prompt;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Prompts;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Services.TextGeneration;
using MyNovelBuilder.WebApi.Services.Tts;

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
        TtsProvider? provider = null)
    {
        TtsProvider ttsProvider;
        
        if (provider.HasValue)
        {
            ttsProvider = provider.Value;
        }
        else
        {
            var config = await _integrationsService.GetConfigAsync();
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
    
    private async ValueTask<ITextGenerationService> GetTextGenerationServiceAsync()
    {
        var config = await _integrationsService.GetConfigAsync();
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
    
    private async Task<string> GetEmphasizedTextAsync(string inputText)
    {
        var textGenerationService = await GetTextGenerationServiceAsync();
        return await textGenerationService
            .GenerateAsync(
                "anthropic/claude-sonnet-4", // TODO: Make configurable
                [
                    new PromptMessageDto
                    {
                        Role = PromptMessageRole.System,
                        Message = SystemPrompts.EmphasizeText
                    },
                    new PromptMessageDto
                    {
                        Role = PromptMessageRole.User,
                        Message = $"Here's the text that needs to be enriched:\n{inputText}"
                    }
                ]
            );
    }

    /// <summary>
    /// Generate audio from text.
    /// </summary>
    [HttpPost("tts")]
    public async Task<ActionResult> GenerateAudioAsync(TtsRequestDto dto)
    {
        var ttsService = await GetTtsServiceAsync();
        
        _logger.LogInformation("Generating audio for text: {Text}", dto.Message);
        
        // Emphasis
        if (ttsService.SupportsEmphasisTags)
        {
            var emphasizedText = await GetEmphasizedTextAsync(dto.Message);
            dto.Message = emphasizedText.Trim();
            
            _logger.LogInformation("Emphasized text: {EmphasizedText}", dto.Message);
        }
        
        var ttsResponse = await ttsService.GenerateAudioAsync(dto);
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
    public async Task<ActionResult> GenerateAudioStreamAsync(TtsRequestDto dto)
    {
        var ttsService = await GetTtsServiceAsync();
        
        _logger.LogInformation("Generating audio stream for text: {Text}", dto.Message);
        
        // Emphasis
        if (ttsService.SupportsEmphasisTags)
        {
            var emphasizedText = await GetEmphasizedTextAsync(dto.Message);
            dto.Message = emphasizedText.Trim();
            
            _logger.LogInformation("Emphasized text: {EmphasizedText}", dto.Message);
        }
        
        Stream audioStream;

        try
        {
            audioStream = await ttsService.GenerateAudioStreamAsync(dto);
        }
        catch (NotImplementedException)
        {
            // Fallback to non-streaming
            var audioBytes = await ttsService.GenerateAudioAsync(dto);
            audioStream = new MemoryStream(audioBytes);
        }
        
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
        
        return File(audioStream, mimeType, fileName);
    }

    /// <summary>
    /// Get available TTS voices.
    /// </summary>
    [HttpGet("tts/voices")]
    public async Task<ActionResult<IEnumerable<TtsVoiceDto>>> GetVoices(
        [FromQuery] TtsProvider? provider)
    {
        var ttsService = await GetTtsServiceAsync(provider);
        
        var voices = await ttsService.GetVoicesAsync();
        
        return Ok(voices);
    }
}
