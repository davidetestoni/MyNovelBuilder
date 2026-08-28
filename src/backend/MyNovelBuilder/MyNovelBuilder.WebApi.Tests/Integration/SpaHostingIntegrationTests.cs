using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MyNovelBuilder.WebApi.Tests.Factories;

namespace MyNovelBuilder.WebApi.Tests.Integration;

public sealed class SpaHostingIntegrationTests : IDisposable,
    IClassFixture<TestWebApplicationFactory<Program>>
{
    private const string IndexContents = "<!doctype html><title>MyNovelBuilder test SPA</title>";

    private readonly string webRoot;
    private readonly WebApplicationFactory<Program> factory;

    public SpaHostingIntegrationTests(TestWebApplicationFactory<Program> baseFactory)
    {
        webRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(webRoot);
        File.WriteAllText(Path.Combine(webRoot, "index.html"), IndexContents);

        factory = baseFactory.WithWebHostBuilder(builder => builder.UseWebRoot(webRoot));
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

    public void Dispose()
    {
        factory.Dispose();
        Directory.Delete(webRoot, recursive: true);
    }
}
