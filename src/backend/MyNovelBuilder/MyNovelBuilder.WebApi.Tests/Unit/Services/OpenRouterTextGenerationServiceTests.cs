using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Extensions;
using MyNovelBuilder.WebApi.Models.Integrations;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Services.TextGeneration;
using MyNovelBuilder.WebApi.Models.Prompts;
using MyNovelBuilder.WebApi.Models.TextGeneration;
using OpenAI.Chat;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public class OpenRouterTextGenerationServiceTests
{
    [Theory]
    [InlineData(ReasoningEffort.None)]
    [InlineData(ReasoningEffort.Low)]
    [InlineData(ReasoningEffort.ProviderDefault)]
    public async Task StreamingUsesIntegrationReasoningDefault(ReasoningEffort effort)
    {
        var handler = new RecordingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                data: {"id":"test","object":"chat.completion.chunk","created":0,"model":"test","choices":[{"index":0,"delta":{"role":"assistant","content":"hello"},"finish_reason":null}]}

                data: [DONE]

                """, Encoding.UTF8, "text/event-stream")
        });
        var service = new OpenRouterTextGenerationService(new HttpClient(handler), new FakeIntegrationsService(effort));
        var chunks = new List<string>();
        await foreach (var chunk in service.GenerateStreamedAsync("test",
            [new PromptMessage { Role = PromptMessageRole.User, Message = "source" }]))
            chunks.Add(chunk);
        Assert.Equal("hello", string.Concat(chunks));
        var payload = JsonNode.Parse(Assert.Single(handler.Bodies))!;
        Assert.True(payload["stream"]!.GetValue<bool>());
        if (effort == ReasoningEffort.ProviderDefault) Assert.Null(payload["reasoning"]);
        else Assert.Equal(effort.ToString().ToLowerInvariant(), payload["reasoning"]!["effort"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(false, ReasoningEffort.None)]
    [InlineData(true, ReasoningEffort.None)]
    [InlineData(true, ReasoningEffort.Low)]
    [InlineData(false, ReasoningEffort.Medium)]
    [InlineData(true, ReasoningEffort.High)]
    [InlineData(true, ReasoningEffort.XHigh)]
    [InlineData(true, ReasoningEffort.Max)]
    [InlineData(true, ReasoningEffort.ProviderDefault)]
    public async Task GenerateWithReasoningAsync_SendsEffortAndPreservesSchema(bool structured, ReasoningEffort effort)
    {
        var handler = new RecordingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {"id":"test","object":"chat.completion","created":0,"model":"deepseek/deepseek-v4-flash",
                 "choices":[{"index":0,"message":{"role":"assistant","content":"result"},"finish_reason":"stop"}]}
                """, Encoding.UTF8, "application/json")
        });
        ITextGenerationService service = new OpenRouterTextGenerationService(new HttpClient(handler), new FakeIntegrationsService());
        var schema = structured ? new StructuredOutputOptions
        {
            SchemaName = "plan", JsonSchema = """{"type":"object","properties":{},"additionalProperties":false}"""
        } : null;

        var result = await service.GenerateWithReasoningAsync("deepseek/deepseek-v4-flash",
            [new PromptMessage { Role = PromptMessageRole.System, Message = "instructions" },
             new PromptMessage { Role = PromptMessageRole.User, Message = "source" }], effort, schema);

        Assert.Equal("result", result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://openrouter.ai/api/v1/chat/completions", request.RequestUri?.ToString());
        Assert.Equal("test-openrouter-key", request.Headers.Authorization?.Parameter);
        var payload = JsonNode.Parse(Assert.Single(handler.Bodies))!;
        if (effort == ReasoningEffort.ProviderDefault) Assert.Null(payload["reasoning"]);
        else
        {
            Assert.Equal(effort != ReasoningEffort.None, payload["reasoning"]!["enabled"]!.GetValue<bool>());
            Assert.Equal(effort.ToString().ToLowerInvariant(), payload["reasoning"]!["effort"]!.GetValue<string>());
        }
        Assert.Equal("deepseek/deepseek-v4-flash", payload["model"]!.GetValue<string>());
        Assert.Equal("system", payload["messages"]![0]!["role"]!.GetValue<string>());
        Assert.Equal("source", payload["messages"]![1]!["content"]!.GetValue<string>());
        if (structured)
        {
            Assert.Equal("json_schema", payload["response_format"]!["type"]!.GetValue<string>());
            Assert.Equal("plan", payload["response_format"]!["json_schema"]!["name"]!.GetValue<string>());
            Assert.True(payload["response_format"]!["json_schema"]!["strict"]!.GetValue<bool>());
        }
        else Assert.Null(payload["response_format"]);
    }

    [Theory]
    [InlineData(ChatFinishReason.Stop)]
    [InlineData(ChatFinishReason.Length)]
    [InlineData(ChatFinishReason.ContentFilter)]
    public void GetResponseText_EmptyContent_ThrowsProviderError(ChatFinishReason finishReason)
    {
        var completion = OpenAIChatModelFactory.ChatCompletion(
            finishReason: finishReason, content: new ChatMessageContent());

        var error = Assert.Throws<ApiException>(() =>
            OpenRouterTextGenerationService.GetResponseText(completion));

        Assert.Equal(ErrorCodes.ExternalServiceError, error.Code);
        Assert.Contains("no text content", error.Message);
        Assert.Contains(finishReason.ToString(), error.Message);
    }

    [Fact]
    public void GetResponseText_NullCompletion_ThrowsProviderError()
    {
        var error = Assert.Throws<ApiException>(() =>
            OpenRouterTextGenerationService.GetResponseText(null));

        Assert.Equal(ErrorCodes.ExternalServiceError, error.Code);
        Assert.Contains("no response", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \n\t")]
    public void GetResponseText_BlankText_ThrowsProviderError(string text)
    {
        var completion = OpenAIChatModelFactory.ChatCompletion(
            content: new ChatMessageContent(text));

        var error = Assert.Throws<ApiException>(() =>
            OpenRouterTextGenerationService.GetResponseText(completion));

        Assert.Equal(ErrorCodes.ExternalServiceError, error.Code);
    }

    [Fact]
    public void GetResponseText_Refusal_ThrowsProviderError()
    {
        var completion = OpenAIChatModelFactory.ChatCompletion(
            content: new ChatMessageContent(), refusal: "Request refused.");

        var error = Assert.Throws<ApiException>(() =>
            OpenRouterTextGenerationService.GetResponseText(completion));

        Assert.Equal(ErrorCodes.ExternalServiceError, error.Code);
        Assert.Contains("Request refused.", error.Message);
    }

    [Fact]
    public void GetResponseText_MultipleParts_PreservesAllTextAndWhitespace()
    {
        var completion = OpenAIChatModelFactory.ChatCompletion(content: new ChatMessageContent(
            ChatMessageContentPart.CreateTextPart("  First"),
            ChatMessageContentPart.CreateTextPart("\nSecond  ")
        ));

        Assert.Equal("  First\nSecond  ", OpenRouterTextGenerationService.GetResponseText(completion));
    }

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

    private sealed class FakeIntegrationsService(ReasoningEffort effort = ReasoningEffort.None) : IIntegrationsService
    {
        public ValueTask<IntegrationsConfig> GetConfigAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new IntegrationsConfig
            {
                OpenRouterApiKey = "test-openrouter-key",
                TextGenerationReasoningEffort = effort
            });

        public Task UpdateConfigAsync(
            IntegrationsConfig config,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri);
            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            Requests.Add(clone);
            if (request.Content is not null)
                Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            return response;
        }
    }
}
