using System.Net;
using Microsoft.Extensions.DependencyInjection;
using MyNovelBuilder.WebApi.Dtos.Chat;
using MyNovelBuilder.WebApi.Extensions;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Tests.Factories;
using Xunit.Abstractions;

namespace MyNovelBuilder.WebApi.Tests.Integration;

public class ValidationIntegrationTests(
    TestWebApplicationFactory<Program> factory,
    ITestOutputHelper output)
    : ControllerIntegrationTests(factory, output), IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await ResetDbAsync();
    }

    [Fact]
    public void PostPutEndpoints_HaveValidatorsForAllInputModels()
    {
        using var scope = Factory.Services.CreateScope();
        var missing = ValidationCoverageExtensions.FindMissingValidators(scope.ServiceProvider);
        Assert.Empty(missing);
    }

    [Fact]
    public async Task InvalidDto_ReturnsBadRequestApiError()
    {
        using var client = Factory.CreateClient();
        var error = await PostJsonAsync(client, "api/chat", new CreateChatDto { NovelId = Guid.Empty });

        Assert.NotNull(error);
        Assert.Equal(HttpStatusCode.BadRequest, error.Response.StatusCode);
        Assert.NotNull(error.Info);
        Assert.Equal(ErrorCodes.BadRequest, error.Info!.Code);
        Assert.Contains("must not be empty", error.Info.Message, StringComparison.OrdinalIgnoreCase);
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
