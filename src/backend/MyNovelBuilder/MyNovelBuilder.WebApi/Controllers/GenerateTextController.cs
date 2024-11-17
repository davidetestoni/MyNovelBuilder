using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Services;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>
/// Controller for generating text.
/// </summary>
[Route("api/generate/text")]
[ApiController]
public class GenerateTextController : ControllerBase
{
    private readonly IPromptCreatorService _promptCreatorService;
    private readonly ITextGenerationService _textGenerationService;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    /// <summary></summary>
    public GenerateTextController(IPromptCreatorService promptCreatorService,
        ITextGenerationService textGenerationService)
    {
        _promptCreatorService = promptCreatorService;
        _textGenerationService = textGenerationService;

        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        _jsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }
    
    /// <summary>
    /// Generate streamed text.
    /// </summary>
    [HttpPost("streamed")]
    public async Task GenerateStreamedTextAsync(GenerateTextRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        HttpContext.Response.Headers.Append("Content-Type", "text/event-stream");
        
        var prompt = await _promptCreatorService.CreatePromptAsync(dto);
        
        await foreach (var chunk in _textGenerationService.GenerateStreamedAsync(dto.Model, prompt, cancellationToken))
        {
            var responseDto = new GenerateTextResponseChunkDto
            {
                Content = chunk
            };
            
            var json = JsonSerializer.Serialize(responseDto, _jsonSerializerOptions);
            
            await HttpContext.Response.WriteAsync(json + "\n", cancellationToken);
            await HttpContext.Response.Body.FlushAsync(cancellationToken);
        }
    }
}