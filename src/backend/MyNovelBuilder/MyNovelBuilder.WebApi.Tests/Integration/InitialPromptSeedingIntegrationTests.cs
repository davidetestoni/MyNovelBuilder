using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Seeding;
using MyNovelBuilder.WebApi.Tests.Factories;

namespace MyNovelBuilder.WebApi.Tests.Integration;

public sealed class InitialPromptSeedingIntegrationTests
{
    [Fact]
    public async Task FreshApplicationStartup_ImportsInitialPromptsOnce()
    {
        using var factory = new TestWebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/prompts");
        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync();
        using var prompts = await JsonDocument.ParseAsync(body);

        Assert.Equal(JsonValueKind.Array, prompts.RootElement.ValueKind);
        Assert.Equal(15, prompts.RootElement.GetArrayLength());
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await dbContext.InitializationMarkers.AnyAsync(
            marker => marker.Key == InitialPromptSeeder.MarkerKey));
    }
}
