using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Extensions;
using MyNovelBuilder.WebApi.Models.Integrations;
using MyNovelBuilder.WebApi.Models.Prompts;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Services.TextGeneration;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public class GoogleGenAiTextGenerationServiceTests
{
    [Fact]
    public async Task KeyedRegistration_ResolvesServiceWithManagedClient()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IIntegrationsService, FakeIntegrationsService>();
        services.RegisterKeyedServicesFromAssembly<ITextGenerationService>();
        await using var provider = services.BuildServiceProvider();

        Assert.IsType<GoogleGenAiTextGenerationService>(
            provider.GetRequiredKeyedService<ITextGenerationService>(TextGenerationProvider.GoogleGenAi));
    }

    [Fact]
    public async Task GenerateStreamedAsync_UsesCancellableProviderRequestAndReadsChunks()
    {
        var handler = new RecordingHandler();
        var service = new GoogleGenAiTextGenerationService(
            new FakeIntegrationsService(), new HttpClient(handler));
        using var cancellation = new CancellationTokenSource();
        var chunks = new List<string>();

        await foreach (var chunk in service.GenerateStreamedAsync(
            "gemini-test", [new PromptMessage { Role = PromptMessageRole.User, Message = "Hello" }],
            cancellationToken: cancellation.Token))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(["Hello", " world"], chunks);
        Assert.True(handler.ObservedToken.CanBeCanceled);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-test:streamGenerateContent?alt=sse",
            handler.RequestUri);
        Assert.Contains("\"contents\"", handler.RequestBody);
    }

    [Fact]
    public async Task GenerateStreamedAsync_CancelsInFlightProviderRequest()
    {
        var handler = new BlockingHandler();
        var service = new GoogleGenAiTextGenerationService(
            new FakeIntegrationsService(), new HttpClient(handler));
        using var cancellation = new CancellationTokenSource();
        await using var enumerator = service.GenerateStreamedAsync(
            "gemini-test", [new PromptMessage { Role = PromptMessageRole.User, Message = "Hello" }],
            cancellationToken: cancellation.Token).GetAsyncEnumerator();

        var moveNext = enumerator.MoveNextAsync().AsTask();
        await handler.Started.Task;
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => moveNext);
        await handler.Cancelled.Task;
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var registration = cancellationToken.Register(() => Cancelled.TrySetResult());
            Started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("The request should have been cancelled.");
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public CancellationToken ObservedToken { get; private set; }
        public string? RequestUri { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ObservedToken = cancellationToken;
            RequestUri = request.RequestUri?.ToString();
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Hello\"}]}}]}\n\n" +
                    "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\" world\"}]},\"finishReason\":\"STOP\"}]}\n\n",
                    Encoding.UTF8, "text/event-stream")
            };
        }
    }

    private sealed class FakeIntegrationsService : IIntegrationsService
    {
        public ValueTask<IntegrationsConfig> GetConfigAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new IntegrationsConfig { GoogleGenAiApiKey = "test-key" });

        public Task UpdateConfigAsync(IntegrationsConfig config, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
