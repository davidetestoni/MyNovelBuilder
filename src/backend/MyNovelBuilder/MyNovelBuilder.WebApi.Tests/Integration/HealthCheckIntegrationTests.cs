using System.Net;
using MyNovelBuilder.WebApi.Tests.Factories;

namespace MyNovelBuilder.WebApi.Tests.Integration;

public sealed class HealthCheckIntegrationTests(
    TestWebApplicationFactory<Program> factory)
    : IClassFixture<TestWebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpointReturnsHealthy(string path)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
    }
}
