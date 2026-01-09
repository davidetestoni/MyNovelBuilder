using System.Text.Json;
using System.Text.Json.Serialization;
using MyNovelBuilder.WebApi.Models.Integrations;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for managing integrations.
/// </summary>
public class IntegrationsService : IIntegrationsService
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private IntegrationsConfig? _cachedConfig;

    /// <summary></summary>
    public IntegrationsService()
    {
        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        _jsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }
    
    /// <inheritdoc />
    public async ValueTask<IntegrationsConfig> GetConfigAsync()
    {
        if (_cachedConfig is not null)
        {
            return _cachedConfig;
        }
        
        var path = Path.Combine(Globals.DataFolder, "integrations.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        string configJson;

        if (!File.Exists(path))
        {
            var defaultConfig = new IntegrationsConfig();
            configJson = JsonSerializer.Serialize(defaultConfig, _jsonSerializerOptions);
            await File.WriteAllTextAsync(path, configJson);
            _cachedConfig = defaultConfig;
            return defaultConfig;
        }
        
        configJson = await File.ReadAllTextAsync(path);
        var config = JsonSerializer.Deserialize<IntegrationsConfig>(configJson, _jsonSerializerOptions)!;
        _cachedConfig = config;
        return config;
    }

    /// <inheritdoc />
    public async Task UpdateConfigAsync(IntegrationsConfig config)
    {
        var path = Path.Combine(Globals.DataFolder, "integrations.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        
        var configJson = JsonSerializer.Serialize(config, _jsonSerializerOptions);
        await File.WriteAllTextAsync(path, configJson);
        _cachedConfig = config;
    }
}
