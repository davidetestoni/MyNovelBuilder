using Microsoft.AspNetCore.Mvc;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Services;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>
/// Controller for generating text, images or audio.
/// </summary>
[Route("api/generate")]
[ApiController]
public class GenerateController : ControllerBase
{
    private readonly IPromptCreatorService _promptCreatorService;
    private readonly ITextGenerationService _textGenerationService;

    /// <summary></summary>
    public GenerateController(IPromptCreatorService promptCreatorService,
        ITextGenerationService textGenerationService)
    {
        _promptCreatorService = promptCreatorService;
        _textGenerationService = textGenerationService;
    }
    
    /// <summary>
    /// Generate streamed text.
    /// </summary>
    [HttpPost("text/streamed")]
    public async IAsyncEnumerable<string> GenerateStreamedTextAsync(GenerateTextRequestDto dto)
    {
        var prompt = await _promptCreatorService.CreatePromptAsync(dto);
        
        await foreach (var chunk in _textGenerationService.GenerateStreamedAsync(prompt))
        {
            yield return chunk;
        }
    }
}
