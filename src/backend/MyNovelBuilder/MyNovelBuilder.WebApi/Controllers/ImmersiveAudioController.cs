using Microsoft.AspNetCore.Mvc;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Services;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>Plans and generates immersive multi-speaker audio.</summary>
[Route("api/generate/audio")]
[ApiController]
public class ImmersiveAudioController(IImmersiveTtsService immersiveTtsService) : ControllerBase
{
    /// <summary>
    /// Prepare immersive TTS chunks without generating audio.
    /// </summary>
    [HttpPost("tts/immersive/debug")]
    public async Task<ActionResult<ImmersiveTtsDebugResponseDto>> DebugImmersiveTtsAsync(
        ImmersiveTtsRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        return Ok(await immersiveTtsService.PrepareDebugAsync(dto, cancellationToken));
    }

    /// <summary>
    /// Generate immersive multi-speaker audio stream for a prose section.
    /// </summary>
    [HttpPost("tts/immersive/stream")]
    public async Task<ActionResult> GenerateImmersiveAudioStreamAsync(
        ImmersiveTtsRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var audioStream = await immersiveTtsService.GenerateStreamAsync(dto, cancellationToken);
        return File(audioStream, "audio/wav", "audio.wav");
    }

}
