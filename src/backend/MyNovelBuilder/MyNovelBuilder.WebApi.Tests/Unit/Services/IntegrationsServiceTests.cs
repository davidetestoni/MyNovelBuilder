using System.Text.Json;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.Integrations;
using MyNovelBuilder.WebApi.Options;
using MyNovelBuilder.WebApi.Services;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public sealed class IntegrationsServiceTests : IDisposable
{
    private readonly string _dataFolder = Path.Combine(
        Path.GetTempPath(),
        nameof(IntegrationsServiceTests),
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task GetConfigAsync_WhenCalledConcurrently_CreatesOneValidConfig()
    {
        var service = CreateService();

        var configs = await Task.WhenAll(
            Enumerable.Range(0, 32)
                .Select(_ => service.GetConfigAsync().AsTask()));

        Assert.All(
            configs,
            config => Assert.Equal(
                IntegrationsConfig.DefaultTextGenerationModelId,
                config.TextGenerationModelId));
        for (var index = 1; index < configs.Length; index++)
        {
            Assert.NotSame(configs[0], configs[index]);
        }

        var persistedConfig = await ReadPersistedConfigAsync();
        Assert.Equal(
            IntegrationsConfig.DefaultTextGenerationModelId,
            persistedConfig.TextGenerationModelId);
        Assert.Empty(GetTemporaryConfigFiles());
    }

    [Fact]
    public async Task GetConfigAsync_ReturnsCopyOfCachedConfig()
    {
        var service = CreateService();
        var firstConfig = await service.GetConfigAsync();

        firstConfig.TextGenerationModelId = "caller-mutation";

        var secondConfig = await service.GetConfigAsync();
        Assert.NotSame(firstConfig, secondConfig);
        Assert.Equal(
            IntegrationsConfig.DefaultTextGenerationModelId,
            secondConfig.TextGenerationModelId);
    }

    [Fact]
    public async Task UpdateConfigAsync_CachesSnapshotOfCallerConfig()
    {
        var service = CreateService();
        var config = new IntegrationsConfig { TextGenerationModelId = "persisted-model" };
        await service.UpdateConfigAsync(config);

        config.TextGenerationModelId = "caller-mutation";

        var cachedConfig = await service.GetConfigAsync();
        Assert.Equal("persisted-model", cachedConfig.TextGenerationModelId);
        Assert.Equal(
            "persisted-model",
            (await ReadPersistedConfigAsync()).TextGenerationModelId);
    }

    [Fact]
    public async Task UpdateConfigAsync_WhenCalledConcurrently_PersistsOneCompleteConfig()
    {
        var service = CreateService();
        var configs = Enumerable.Range(0, 32)
            .Select(index => new IntegrationsConfig
            {
                OpenRouterApiKey = $"api-key-{index}-{new string('x', 16_384)}",
                TextGenerationModelId = $"model-{index}"
            })
            .ToArray();

        await Task.WhenAll(configs.Select(config => service.UpdateConfigAsync(config)));

        var persistedConfig = await ReadPersistedConfigAsync();
        var expectedConfig = Assert.Single(
            configs,
            config => config.TextGenerationModelId == persistedConfig.TextGenerationModelId);
        Assert.Equal(expectedConfig.OpenRouterApiKey, persistedConfig.OpenRouterApiKey);

        var cachedConfig = await service.GetConfigAsync();
        Assert.Equal(persistedConfig.TextGenerationModelId, cachedConfig.TextGenerationModelId);
        Assert.Equal(persistedConfig.OpenRouterApiKey, cachedConfig.OpenRouterApiKey);
        Assert.Empty(GetTemporaryConfigFiles());
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataFolder))
        {
            Directory.Delete(_dataFolder, recursive: true);
        }
    }

    private IntegrationsService CreateService()
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new AppStorageOptions { DataFolder = _dataFolder });
        return new IntegrationsService(options);
    }

    private async Task<IntegrationsConfig> ReadPersistedConfigAsync()
    {
        var json = await File.ReadAllTextAsync(
            Path.Combine(_dataFolder, "integrations.json"));
        return JsonSerializer.Deserialize<IntegrationsConfig>(json, JsonDefaults.Options)!;
    }

    private IEnumerable<string> GetTemporaryConfigFiles() =>
        Directory.EnumerateFiles(_dataFolder, ".integrations.json.*.tmp");
}
