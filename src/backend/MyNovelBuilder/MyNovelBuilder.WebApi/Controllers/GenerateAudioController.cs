using Microsoft.AspNetCore.Mvc;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Services;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>
/// Controller for generating audio.
/// </summary>
[Route("api/generate/audio")]
[ApiController]
public class GenerateAudioController : ControllerBase
{
    private readonly ITtsService _ttsService;

    /// <summary></summary>
    public GenerateAudioController(ITtsService ttsService)
    {
        _ttsService = ttsService;
    }
    
    /// <summary>
    /// Generate audio from text.
    /// </summary>
    [HttpPost("tts")]
    public async Task<ActionResult> GenerateAudioAsync(TtsRequestDto dto)
    {
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
