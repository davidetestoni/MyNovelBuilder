using System.Text.Json;
using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.Integrations;
using MyNovelBuilder.WebApi.Options;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for managing integrations.
/// </summary>
public class IntegrationsService : IIntegrationsService
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly string _dataFolder;
    private IntegrationsConfig? _cachedConfig;

    /// <summary></summary>
    public IntegrationsService(IOptions<AppStorageOptions> storageOptions)
    {
        _jsonSerializerOptions = JsonDefaults.Options;
        _dataFolder = storageOptions.Value.DataFolder;
    }
    
    /// <inheritdoc />
    public async ValueTask<IntegrationsConfig> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedConfig is not null)
        {
            return _cachedConfig;
        }
        
        var path = Path.Combine(_dataFolder, "integrations.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        string configJson;

        if (!File.Exists(path))
        {
            var defaultConfig = new IntegrationsConfig();
            configJson = JsonSerializer.Serialize(defaultConfig, _jsonSerializerOptions);
            await File.WriteAllTextAsync(path, configJson, cancellationToken);
            _cachedConfig = defaultConfig;
            return defaultConfig;
        }
        
        configJson = await File.ReadAllTextAsync(path, cancellationToken);
        var config = JsonSerializer.Deserialize<IntegrationsConfig>(configJson, _jsonSerializerOptions)!;
        _cachedConfig = config;
        return config;
    }

    /// <inheritdoc />
    public async Task UpdateConfigAsync(IntegrationsConfig config, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_dataFolder, "integrations.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        
        var configJson = JsonSerializer.Serialize(config, _jsonSerializerOptions);
        await File.WriteAllTextAsync(path, configJson, cancellationToken);
        _cachedConfig = config;
    }
}
