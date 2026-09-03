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
    private const string ConfigFileName = "integrations.json";

    private readonly SemaphoreSlim _configLock = new(1, 1);
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
        var cachedConfig = Volatile.Read(ref _cachedConfig);
        if (cachedConfig is not null)
        {
            return cachedConfig.Copy();
        }

        await _configLock.WaitAsync(cancellationToken);
        try
        {
            cachedConfig = Volatile.Read(ref _cachedConfig);
            if (cachedConfig is not null)
            {
                return cachedConfig.Copy();
            }

            var path = GetConfigPath();
            Directory.CreateDirectory(_dataFolder);

            IntegrationsConfig config;
            if (!File.Exists(path))
            {
                config = new IntegrationsConfig();
                await WriteConfigAtomicallyAsync(path, config, cancellationToken);
            }
            else
            {
                var configJson = await File.ReadAllTextAsync(path, cancellationToken);
                config = JsonSerializer.Deserialize<IntegrationsConfig>(
                    configJson,
                    _jsonSerializerOptions)!;
            }

            Volatile.Write(ref _cachedConfig, config);
            return config.Copy();
        }
        finally
        {
            _configLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task UpdateConfigAsync(IntegrationsConfig config, CancellationToken cancellationToken = default)
    {
        var configSnapshot = config.Copy();
        await _configLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_dataFolder);
            await WriteConfigAtomicallyAsync(GetConfigPath(), configSnapshot, cancellationToken);
            Volatile.Write(ref _cachedConfig, configSnapshot);
        }
        finally
        {
            _configLock.Release();
        }
    }

    private string GetConfigPath() => Path.Combine(_dataFolder, ConfigFileName);

    private async Task WriteConfigAtomicallyAsync(
        string path,
        IntegrationsConfig config,
        CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(
            _dataFolder,
            $".{ConfigFileName}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                             tempPath,
                             new FileStreamOptions
                             {
                                 Mode = FileMode.CreateNew,
                                 Access = FileAccess.Write,
                                 Share = FileShare.None,
                                 Options = FileOptions.Asynchronous | FileOptions.WriteThrough
                             }))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    config,
                    _jsonSerializerOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
