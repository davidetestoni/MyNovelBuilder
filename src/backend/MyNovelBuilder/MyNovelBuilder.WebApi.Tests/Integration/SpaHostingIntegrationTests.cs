using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Options;
using MyNovelBuilder.WebApi.Tests.Factories;

namespace MyNovelBuilder.WebApi.Tests.Integration;

public sealed class SpaHostingIntegrationTests : IDisposable,
    IClassFixture<TestWebApplicationFactory<Program>>
{
    private const string IndexContents = "<!doctype html><title>MyNovelBuilder test SPA</title>";

    private readonly string webRoot;
    private readonly WebApplicationFactory<Program> factory;
    private readonly string staticTestFile;

    public SpaHostingIntegrationTests(TestWebApplicationFactory<Program> baseFactory)
    {
        webRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(webRoot);
        File.WriteAllText(Path.Combine(webRoot, "index.html"), IndexContents);

        factory = baseFactory.WithWebHostBuilder(builder => builder.UseWebRoot(webRoot));

        var storageOptions = factory.Services
            .GetRequiredService<IOptions<AppStorageOptions>>()
            .Value;
        Directory.CreateDirectory(storageOptions.StaticFilesRoot);
        staticTestFile = Path.Combine(storageOptions.StaticFilesRoot, "hosting-smoke.txt");
        File.WriteAllText(staticTestFile, "User-owned static content");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/novels")]
    [InlineData("/novel/00000000-0000-0000-0000-000000000000/settings")]
    public async Task SpaRoutesReturnIndex(string path)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        response.EnsureSuccessStatusCode();
        Assert.Equal(IndexContents, await response.Content.ReadAsStringAsync());
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("/api/does-not-exist")]
    [InlineData("/static/does-not-exist")]
    [InlineData("/health/does-not-exist")]
    public async Task ReservedRoutesAreNotHandledBySpaFallback(string path)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual(IndexContents, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task StaticRouteServesUserOwnedFile()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/static/hosting-smoke.txt");

        response.EnsureSuccessStatusCode();
        Assert.Equal("User-owned static content", await response.Content.ReadAsStringAsync());
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
    }

    public void Dispose()
    {
        factory.Dispose();
        File.Delete(staticTestFile);
        Directory.Delete(webRoot, recursive: true);
    }
}
