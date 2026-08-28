using System.Net;
using MyNovelBuilder.WebApi.Tests.Factories;

namespace MyNovelBuilder.WebApi.Tests.Integration;

public sealed class LocalHostingSecurityIntegrationTests(
    TestWebApplicationFactory<Program> factory)
    : IClassFixture<TestWebApplicationFactory<Program>>
{
    [Fact]
    public async Task SwaggerDocumentIsNotExposedOutsideDevelopment()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ApiDoesNotGrantCrossOriginAccess()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/novels");
        request.Headers.Add("Origin", "https://untrusted.example");

        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
