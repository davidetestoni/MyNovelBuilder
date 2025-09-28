using Microsoft.AspNetCore.Mvc;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Dtos.Prompt;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Prompts;
using MyNovelBuilder.WebApi.Services;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>
/// Controller for generating audio.
/// </summary>
[Route("api/generate/audio")]
[ApiController]
public class GenerateAudioController : ControllerBase
{
    private readonly ILogger<GenerateAudioController> _logger;
    private readonly ITtsService _ttsService;
    private readonly ITextGenerationService _textGenerationService;

    /// <summary></summary>
    public GenerateAudioController(
        ILogger<GenerateAudioController> logger,
        ITtsService ttsService,
        ITextGenerationService textGenerationService)
    {
        _logger = logger;
        _ttsService = ttsService;
        _textGenerationService = textGenerationService;
    }
    
    /// <summary>
    /// Generate audio from text.
    /// </summary>
    [HttpPost("tts")]
    public async Task<ActionResult> GenerateAudioAsync(TtsRequestDto dto)
    {
        _logger.LogInformation("Generating audio for text: {Text}", dto.Message);
        
        // Emphasis
        if (_ttsService.SupportsEmphasisTags)
        {
            var emphasizedText = await _textGenerationService
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
                            Message = $"Here's the text that needs to be enriched:\n{dto.Message}"
                        }
                    ]
                );
            dto.Message = emphasizedText.Trim();
            
            _logger.LogInformation("Emphasized text: {EmphasizedText}", dto.Message);
        }
        
        var ttsResponse = await _ttsService.GenerateAudioAsync(dto);
        
        return File(ttsResponse, "audio/mpeg", "audio.mp3");
    }

    /// <summary>
    /// Get available TTS voices.
    /// </summary>
    [HttpGet("tts/voices")]
    public async Task<ActionResult<IEnumerable<TtsVoiceDto>>> GetVoices()
    {
        var voices = await _ttsService.GetVoicesAsync();
        
        return Ok(voices);
    }
}
