using MyNovelBuilder.WebApi.Models.Integrations;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for managing integrations.
/// </summary>
public interface IIntegrationsService
{
    /// <summary>
    /// Get the integrations' configuration.
    /// </summary>
    ValueTask<IntegrationsConfig> GetConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Update the integrations' configuration.
    /// </summary>
    Task UpdateConfigAsync(IntegrationsConfig config, CancellationToken cancellationToken = default);
}
