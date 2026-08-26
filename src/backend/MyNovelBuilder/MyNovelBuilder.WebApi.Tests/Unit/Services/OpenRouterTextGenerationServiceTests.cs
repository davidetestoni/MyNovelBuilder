using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Extensions;
using MyNovelBuilder.WebApi.Models.Integrations;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Services.TextGeneration;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public class OpenRouterTextGenerationServiceTests
{
    [Fact]
    public async Task KeyedRegistration_ResolvesServiceWithManagedClient()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IIntegrationsService, FakeIntegrationsService>();
        services.RegisterKeyedServicesFromAssembly<ITextGenerationService>();
        await using var provider = services.BuildServiceProvider();

        var service = provider.GetRequiredKeyedService<ITextGenerationService>(
            TextGenerationProvider.OpenRouter);

        Assert.IsType<OpenRouterTextGenerationService>(service);
    }

    [Fact]
    public async Task GetAvailableModelsAsync_UsesManagedClientAndMapsCapabilities()
    {
        var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "data": [
                        {
                          "id": "example/vision-model",
                          "architecture": {
                            "input_modalities": ["text", "image"]
                          },
                          "supported_parameters": ["structured_outputs"],
                          "pricing": {
                            "prompt": "0.000001",
                            "completion": "0.000002"
                          }
                        }
                      ]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });
        var service = new OpenRouterTextGenerationService(
            new HttpClient(handler),
            new FakeIntegrationsService());

        var model = Assert.Single(await service.GetAvailableModelsAsync());

        Assert.Equal("example/vision-model", model.Id);
        Assert.True(model.IsVisionCapable);
        Assert.True(model.SupportsStructuredOutputs);
        Assert.Equal(0.000001m, model.InputTokenPrice);
        Assert.Equal(0.000002m, model.OutputTokenPrice);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://openrouter.ai/api/v1/models", request.RequestUri?.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("test-openrouter-key", request.Headers.Authorization?.Parameter);
    }

    private sealed class FakeIntegrationsService : IIntegrationsService
    {
        public ValueTask<IntegrationsConfig> GetConfigAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new IntegrationsConfig
            {
                OpenRouterApiKey = "test-openrouter-key"
            });

        public Task UpdateConfigAsync(
            IntegrationsConfig config,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri);
            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            Requests.Add(clone);
            return Task.FromResult(response);
        }
    }
}
