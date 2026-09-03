using Mapster;
using Microsoft.AspNetCore.Mvc;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Voice;
using MyNovelBuilder.WebApi.Services;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>
/// Controller for voices.
/// </summary>
[Route("api/voices")]
[ApiController]
public class VoiceController : ControllerBase
{
    private readonly IVoiceService _voiceService;

    /// <summary></summary>
    public VoiceController(IVoiceService voiceService)
    {
        _voiceService = voiceService;
    }

    /// <summary>
    /// Get all voices.
    /// </summary>
    [HttpGet]
    public async Task<IEnumerable<VoiceDto>> GetAllVoices(CancellationToken cancellationToken = default)
    {
        var voices = await _voiceService.GetAllAsync(cancellationToken);
        return voices.Adapt<IEnumerable<VoiceDto>>();
    }

    /// <summary>
    /// Create a voice.
    /// </summary>
    [HttpPost]
    public async Task<VoiceDto> CreateVoice(
        [FromForm] CreateVoiceDto createVoiceDto,
        CancellationToken cancellationToken = default)
    {
        var voice = createVoiceDto.Adapt<Voice>();
        await _voiceService.CreateAsync(voice, createVoiceDto.File, cancellationToken);
        return voice.Adapt<VoiceDto>();
    }

    /// <summary>
    /// Update a voice.
    /// </summary>
    [HttpPut]
    public async Task<VoiceDto> UpdateVoice(
        [FromForm] UpdateVoiceDto updateVoiceDto,
        CancellationToken cancellationToken = default)
    {
        var voice = await _voiceService.GetByIdAsync(updateVoiceDto.Id, cancellationToken);
        updateVoiceDto.Adapt(voice);
        await _voiceService.UpdateAsync(voice, updateVoiceDto.File, cancellationToken);
        return voice.Adapt<VoiceDto>();
    }

    /// <summary>
    /// Delete a voice by its ID.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task DeleteVoice(Guid id, CancellationToken cancellationToken = default)
    {
        await _voiceService.DeleteAsync(id, cancellationToken);
    }
    
    /// <summary>
    /// Get a preview of the voice sample WAV file.
    /// </summary>
    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> GetVoicePreview(
        Guid id,
        [FromQuery] int seconds = 5,
        CancellationToken cancellationToken = default)
    {
        var preview = await _voiceService.GetPreviewAsync(id, seconds, cancellationToken);
        return File(preview, "audio/wav", "preview.wav");
    }
}
