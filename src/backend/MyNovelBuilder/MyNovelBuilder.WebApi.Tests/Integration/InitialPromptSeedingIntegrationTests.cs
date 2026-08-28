using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Dtos.Prompt;
using MyNovelBuilder.WebApi.Seeding;
using MyNovelBuilder.WebApi.Tests.Factories;
using Xunit.Abstractions;

namespace MyNovelBuilder.WebApi.Tests.Integration;

public sealed class InitialPromptSeedingIntegrationTests(
    TestWebApplicationFactory<Program> factory,
    ITestOutputHelper output)
    : ControllerIntegrationTests(factory, output)
{
    [Fact]
    public async Task FreshApplicationStartup_ImportsInitialPromptsOnce()
    {
        using var client = Factory.CreateClient();

        var result = await GetJsonAsync<List<PromptDto>>(client, "/api/prompts");

        Assert.True(result.IsOk);
        Assert.Equal(15, result.Value.Count);
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await dbContext.InitializationMarkers.AnyAsync(
            marker => marker.Key == InitialPromptSeeder.MarkerKey));
    }
}
