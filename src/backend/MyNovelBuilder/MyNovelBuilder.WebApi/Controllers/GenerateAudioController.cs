using Microsoft.AspNetCore.Mvc;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Services;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>Generates speech audio from text.</summary>
[Route("api/generate/audio")]
[ApiController]
public class GenerateAudioController(
    ILogger<GenerateAudioController> logger,
    ITtsAudioGenerationService ttsAudioGenerationService) : ControllerBase
{
    /// <summary>
    /// Generate audio from text.
    /// </summary>
    [HttpPost("tts")]
    public async Task<ActionResult> GenerateAudioAsync(
        TtsRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Generating audio for textLength={TextLength}.", dto.Message.Length);
        logger.LogDebug("Generating audio for text: {Text}", dto.Message);
        var wavBytes = await ttsAudioGenerationService.GenerateWavBytesAsync(
            new TextToSpeechGenerationRequest
            {
                Message = dto.Message,
                Provider = dto.Provider,
                TtsModelId = dto.ModelId,
                GainDb = dto.GainDb,
                VoiceId = dto.VoiceId
            },
            cancellationToken);

        return File(wavBytes, "audio/wav", "audio.wav");
    }

    /// <summary>
    /// Generate audio stream from text.
    /// </summary>
    [HttpPost("tts/stream")]
    public async Task<ActionResult> GenerateAudioStreamAsync(
        TtsRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Generating audio stream for textLength={TextLength}.", dto.Message.Length);
        logger.LogDebug("Generating audio stream for text: {Text}", dto.Message);
        var audioStream = await ttsAudioGenerationService.GenerateWavStreamAsync(
            new TextToSpeechGenerationRequest
            {
                Message = dto.Message,
                Provider = dto.Provider,
                TtsModelId = dto.ModelId,
                GainDb = dto.GainDb,
                VoiceId = dto.VoiceId
            },
            cancellationToken);

        return File(audioStream, "audio/wav", "audio.wav");
    }

}
