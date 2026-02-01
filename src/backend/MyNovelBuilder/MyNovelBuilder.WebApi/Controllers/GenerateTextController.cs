using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Services.TextGeneration;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>
/// Controller for generating text.
/// </summary>
[Route("api/generate/text")]
[ApiController]
public class GenerateTextController : ControllerBase
{
    private readonly ILogger<GenerateTextController> _logger;
    private readonly IPromptCreatorService _promptCreatorService;
    private readonly IServiceProvider _keyedProvider;
    private readonly IIntegrationsService _integrationsService;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    /// <summary></summary>
    public GenerateTextController(
        ILogger<GenerateTextController> logger,
        IPromptCreatorService promptCreatorService,
        IServiceProvider keyedProvider,
        IIntegrationsService integrationsService)
    {
        _logger = logger;
        _promptCreatorService = promptCreatorService;
        _keyedProvider = keyedProvider;
        _integrationsService = integrationsService;

        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        _jsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }
    
    private async ValueTask<ITextGenerationService> GetTextGenerationServiceAsync()
    {
        var config = await _integrationsService.GetConfigAsync();
        var textGenerationService = _keyedProvider.GetKeyedService<ITextGenerationService>(config.TextGenerationProvider);

        if (textGenerationService is null)
        {
            _logger.LogError(
                "Unsupported text generation provider: {Provider}", config.TextGenerationProvider);
            
            throw new InvalidOperationException(
                $"Unsupported text generation provider: {config.TextGenerationProvider}");
        }

        return textGenerationService;
    }
    
    /// <summary>
    /// Generate streamed text.
    /// </summary>
    [HttpPost("streamed")]
    public async Task GenerateStreamedTextAsync(GenerateTextRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var textGenerationService = await GetTextGenerationServiceAsync();
        
        HttpContext.Response.Headers.Append("Content-Type", "text/event-stream");
        
        var prompt = await _promptCreatorService.CreatePromptAsync(dto);
        
        await foreach (var chunk in textGenerationService.GenerateStreamedAsync(dto.Model, prompt, cancellationToken))
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
    
    /// <summary>
    /// Get available text generation models.
    /// </summary>
    [HttpGet("models")]
    public async Task<IEnumerable<TextGenerationModelInfoDto>> GetAvailableModelsAsync()
    {
        var textGenerationService = await GetTextGenerationServiceAsync();
        var models = await textGenerationService.GetAvailableModelsAsync();
        
        return models.Select(m => new TextGenerationModelInfoDto
        {
            Id = m.Id
        });
    }
}