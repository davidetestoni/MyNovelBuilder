using Microsoft.AspNetCore.Mvc;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Dtos.Prompt;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Prompts;
using MyNovelBuilder.WebApi.Services;
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
    private readonly IKeyedServiceProvider _keyedProvider;
    private readonly ITextGenerationService _textGenerationService;
    private readonly IIntegrationsService _integrationsService;

    /// <summary></summary>
    public GenerateAudioController(
        ILogger<GenerateAudioController> logger,
        IKeyedServiceProvider keyedProvider,
        ITextGenerationService textGenerationService,
        IIntegrationsService integrationsService)
    {
        _logger = logger;
        _keyedProvider = keyedProvider;
        _textGenerationService = textGenerationService;
        _integrationsService = integrationsService;
    }
    
    private async Task<ITtsService> GetTtsServiceAsync()
    {
        var config = await _integrationsService.GetConfigAsync();
        var ttsService = _keyedProvider.GetKeyedService<ITtsService>(config.TtsProvider);

        if (ttsService is null)
        {
            _logger.LogError(
                "Unsupported TTS provider: {Provider}", config.TtsProvider);

            throw new InvalidOperationException(
                $"Unsupported TTS provider: {config.TtsProvider}");
        }

        return ttsService;
    }
    
    private async Task<string> GetEmphasizedTextAsync(string inputText) =>
        await _textGenerationService
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
    public async Task<ActionResult<IEnumerable<TtsVoiceDto>>> GetVoices()
    {
        var ttsService = await GetTtsServiceAsync();
        
        var voices = await ttsService.GetVoicesAsync();
        
        return Ok(voices);
    }
}
