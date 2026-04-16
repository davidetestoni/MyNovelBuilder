using MyNovelBuilder.WebApi.Services.TextGeneration;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Resolves the configured text-generation provider service.
/// </summary>
public class TextGenerationServiceResolver : ITextGenerationServiceResolver
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IIntegrationsService _integrationsService;
    private readonly ILogger<TextGenerationServiceResolver> _logger;

    /// <summary></summary>
    public TextGenerationServiceResolver(
        IServiceProvider serviceProvider,
        IIntegrationsService integrationsService,
        ILogger<TextGenerationServiceResolver> logger)
    {
        _serviceProvider = serviceProvider;
        _integrationsService = integrationsService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<ITextGenerationService> GetConfiguredServiceAsync(
        CancellationToken cancellationToken = default)
    {
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        var textGenerationService =
            _serviceProvider.GetKeyedService<ITextGenerationService>(config.TextGenerationProvider);

        if (textGenerationService is not null)
        {
            return textGenerationService;
        }

        _logger.LogError(
            "Unsupported text generation provider: {Provider}",
            config.TextGenerationProvider);

        throw new InvalidOperationException(
            $"Unsupported text generation provider: {config.TextGenerationProvider}");
    }
}
