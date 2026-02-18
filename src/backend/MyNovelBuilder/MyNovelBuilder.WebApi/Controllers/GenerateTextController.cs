using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Dtos.Prompt;
using MyNovelBuilder.WebApi.Exceptions;
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
    private readonly INovelPromptCreatorService _novelPromptCreatorService;
    private readonly ICompendiumPromptCreatorService _compendiumPromptCreatorService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IIntegrationsService _integrationsService;
    private readonly HybridCache _hybridCache;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    /// <summary></summary>
    public GenerateTextController(
        ILogger<GenerateTextController> logger,
        INovelPromptCreatorService novelPromptCreatorService,
        ICompendiumPromptCreatorService compendiumPromptCreatorService,
        IServiceProvider serviceProvider,
        IIntegrationsService integrationsService,
        HybridCache hybridCache)
    {
        _logger = logger;
        _novelPromptCreatorService = novelPromptCreatorService;
        _compendiumPromptCreatorService = compendiumPromptCreatorService;
        _serviceProvider = serviceProvider;
        _integrationsService = integrationsService;
        _hybridCache = hybridCache;

        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        _jsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
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
    
    /// <summary>
    /// Generate streamed text.
    /// </summary>
    [HttpPost("streamed")]
    public async Task GenerateStreamedTextAsync(GenerateTextRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var textGenerationService = await GetTextGenerationServiceAsync();
        
        HttpContext.Response.Headers.Append("Content-Type", "text/event-stream");

        var prompt = dto.ContextInfo switch
        {
            NovelTextGenerationContextInfoDto => await _novelPromptCreatorService.CreatePromptAsync(dto),
            CompendiumTextGenerationContextInfoDto => await _compendiumPromptCreatorService.CreatePromptAsync(dto),
            _ => throw new ApiException(ErrorCodes.InvalidPromptContext, "The prompt context is invalid.")
        };

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
    /// Describe an image.
    /// </summary>
    [HttpPost("describe-image")]
    public async Task<ActionResult<string>> DescribeImageAsync(
        IFormFile image,
        [FromForm] DescribeImageRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        if (image.Length == 0)
        {
            return BadRequest("Image file is required.");
        }

        await using var imageStream = image.OpenReadStream();
        using var ms = new MemoryStream();
        await imageStream.CopyToAsync(ms, cancellationToken);
        var imageBytes = ms.ToArray();

        var prompt = await _compendiumPromptCreatorService.CreatePromptAsync(
            new GenerateTextRequestDto
            {
                Model = dto.Model,
                PromptId = dto.PromptId,
                ContextInfo = new DescribeImageContextInfoDto
                {
                    CompendiumId = dto.CompendiumId,
                    Instructions = dto.Instructions
                }
            });
        
        var textGenerationService = await GetTextGenerationServiceAsync();
        var description = await textGenerationService.DescribeImageAsync(
            dto.Model,
            prompt,
            imageBytes,
            string.IsNullOrWhiteSpace(image.ContentType) ? "image/png" : image.ContentType,
            cancellationToken);

        return Ok(description);
    }
    
    /// <summary>
    /// Get available text generation models.
    /// </summary>
    [HttpGet("models")]
    public async Task<IEnumerable<TextGenerationModelInfoDto>> GetAvailableModelsAsync()
    {
        var config = await _integrationsService.GetConfigAsync();
        return await _hybridCache.GetOrCreateAsync(
            $"textgen-{config.TextGenerationProvider}-models",
            async _ =>
            {
                var textGenerationService = await GetTextGenerationServiceAsync();
                var models = await textGenerationService.GetAvailableModelsAsync();
        
                return models.Select(m => new TextGenerationModelInfoDto
                {
                    Id = m.Id,
                    IsVisionCapable = m.IsVisionCapable
                });
            },
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromHours(6),
                LocalCacheExpiration = TimeSpan.FromHours(6)
            },
            tags: ["textgen", config.TextGenerationProvider.ToString(), "models"]
        );
    }
}
