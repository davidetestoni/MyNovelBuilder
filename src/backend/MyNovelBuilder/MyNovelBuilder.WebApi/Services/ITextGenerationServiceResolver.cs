using MyNovelBuilder.WebApi.Services.TextGeneration;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Resolves the configured text-generation service implementation.
/// </summary>
public interface ITextGenerationServiceResolver
{
    /// <summary>
    /// Get the configured text-generation service.
    /// </summary>
    ValueTask<ITextGenerationService> GetConfiguredServiceAsync(CancellationToken cancellationToken = default);
}
