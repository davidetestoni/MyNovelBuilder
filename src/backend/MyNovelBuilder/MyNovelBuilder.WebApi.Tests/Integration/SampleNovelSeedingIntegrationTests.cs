using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Dtos.Novel;
using MyNovelBuilder.WebApi.Options;
using MyNovelBuilder.WebApi.Seeding;
using MyNovelBuilder.WebApi.Tests.Factories;
using Xunit.Abstractions;

namespace MyNovelBuilder.WebApi.Tests.Integration;

public sealed class SampleNovelSeedingIntegrationTests(
    TestWebApplicationFactory<Program> factory,
    ITestOutputHelper output)
    : ControllerIntegrationTests(factory, output)
{
    [Fact]
    public async Task ApplicationStartup_ImportsSampleNovelAndMedia()
    {
        using var client = Factory.CreateClient();

        var result = await GetJsonAsync<List<NovelDto>>(client, "/api/novels");

        Assert.True(result.IsOk);
        var sample = Assert.Single(
            result.Value,
            novel => novel.Title.Contains("Sample Novel", StringComparison.Ordinal));
        Assert.False(sample.RpgMode);
        Assert.NotNull(sample.CoverImageUrl);

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await dbContext.InitializationMarkers.AnyAsync(
            marker => marker.Key == SampleNovelSeeder.MarkerKey));

        var storageOptions = scope.ServiceProvider
            .GetRequiredService<IOptions<AppStorageOptions>>()
            .Value;
        Assert.True(File.Exists(Path.Combine(
            storageOptions.DataFolder,
            "novels",
            sample.Id.ToString(),
            "prose.json")));
        Assert.Equal(
            2,
            Directory.GetFiles(
                Path.Combine(
                    storageOptions.DataFolder,
                    "static",
                    "novels",
                    sample.Id.ToString()),
                "*.png",
                SearchOption.AllDirectories).Length);
    }
}
