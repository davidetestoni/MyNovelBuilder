using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.Integrations;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Services.ImageGeneration;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public class OpenRouterImageGenerationServiceTests
{
    [Fact]
    public async Task GetAvailableModelsAsync_MergesCatalogsAndPrefersImagesCapabilities()
    {
        var handler = new QueueingHttpMessageHandler(
        [
            CreateJsonResponse(
                """
                {
                  "data": [
                    {
                      "id": "black-forest-labs/flux.2-pro",
                      "name": "Flux Pro",
                      "architecture": {
                        "input_modalities": ["text", "image"],
                        "output_modalities": ["image"]
                      },
                      "supported_parameters": {}
                    }
                  ]
                }
                """),
            CreateJsonResponse(
                """
                {
                  "data": [
                    {
                      "id": "google/gemini-2.5-flash-image",
                      "name": "Gemini Image",
                      "architecture": {
                        "input_modalities": ["text", "image"],
                        "output_modalities": ["text", "image"]
                      }
                    },
                    {
                      "id": "black-forest-labs/flux.2-pro",
                      "name": "Duplicate Flux Pro",
                      "architecture": {
                        "input_modalities": ["text", "image"],
                        "output_modalities": ["image"]
                      }
                    }
                  ]
                }
                """)
        ]);
        var service = CreateService(handler);

        var models = (await service.GetAvailableModelsAsync()).ToList();

        Assert.Equal(
            new[] { "/api/v1/images/models", "/api/v1/models?output_modalities=image" },
            handler.Requests.Select(request => request.RequestUri!.PathAndQuery));
        Assert.Collection(
            models,
            model =>
            {
                Assert.Equal("black-forest-labs/flux.2-pro", model.ModelId);
                Assert.Equal("Flux Pro", model.Name);
                Assert.True(model.SupportsImageGeneration);
                Assert.False(model.SupportsImageEditing);
            },
            model =>
            {
                Assert.Equal("google/gemini-2.5-flash-image", model.ModelId);
                Assert.Equal("Gemini Image", model.Name);
                Assert.True(model.SupportsImageGeneration);
                Assert.True(model.SupportsImageEditing);
            });
    }

    [Fact]
    public async Task GenerateImageAsync_UsesModelModalities_AndDecodesReturnedDataUrl()
    {
        var imageBytes = Encoding.ASCII.GetBytes("generated-image");
        var handler = new QueueingHttpMessageHandler(
        [
            CreateJsonResponse("""{"data": []}"""),
            CreateJsonResponse(
                """
                {
                  "data": [
                    {
                      "id": "google/gemini-2.5-flash-image",
                      "name": "Gemini Image",
                      "architecture": {
                        "input_modalities": ["text", "image"],
                        "output_modalities": ["text", "image"]
                      }
                    }
                  ]
                }
                """),
            CreateJsonResponse(
                $$"""
                {
                  "choices": [
                    {
                      "message": {
                        "images": [
                          {
                            "image_url": {
                              "url": "data:image/png;base64,{{Convert.ToBase64String(imageBytes)}}"
                            }
                          }
                        ]
                      }
                    }
                  ]
                }
                """)
        ]);
        var service = CreateService(handler);

        var result = await service.GenerateImageAsync(new ImageGenerationRequestDto
        {
            ModelId = "google/gemini-2.5-flash-image",
            Prompt = "A lantern-lit alley in the rain",
            Width = 832,
            Height = 1248
        });

        Assert.Equal(imageBytes, result);

        var request = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post);
        Assert.Equal("https://openrouter.ai/api/v1/chat/completions", request.RequestUri?.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("test-openrouter-key", request.Headers.Authorization?.Parameter);

        using var requestJson = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
        Assert.Equal(
            "google/gemini-2.5-flash-image",
            requestJson.RootElement.GetProperty("model").GetString());
        Assert.Equal(
            new[] { "image", "text" },
            requestJson.RootElement.GetProperty("modalities")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray());
        Assert.Equal(
            "A lantern-lit alley in the rain",
            requestJson.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
        Assert.Equal(
            "2:3",
            requestJson.RootElement.GetProperty("image_config").GetProperty("aspect_ratio").GetString());
    }

    [Fact]
    public async Task GenerateImageAsync_UsesImagesEndpoint_AndDecodesBase64()
    {
        var handler = new QueueingHttpMessageHandler(
        [
            CreateJsonResponse(
                """
                {"data": [{
                  "id": "bytedance-seed/seedream-4.5",
                  "supported_parameters": {"aspect_ratio": {"type": "enum", "values": ["2:3"]}}
                }]}
                """),
            CreateJsonResponse("""{"data": [{"b64_json": "AQID", "media_type": "image/png"}]}""")
        ]);
        var service = CreateService(handler);

        var result = await service.GenerateImageAsync(new ImageGenerationRequestDto
        {
            ModelId = "bytedance-seed/seedream-4.5",
            Prompt = "A lantern-lit alley in the rain",
            Width = 832,
            Height = 1248
        });

        Assert.Equal(new byte[] { 1, 2, 3 }, result);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("/api/v1/images/models", handler.Requests[0].RequestUri?.PathAndQuery);
        var request = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post);
        Assert.Equal("/api/v1/images", request.RequestUri?.PathAndQuery);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("test-openrouter-key", request.Headers.Authorization?.Parameter);

        using var requestJson = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
        var payload = requestJson.RootElement;
        Assert.Equal("bytedance-seed/seedream-4.5", payload.GetProperty("model").GetString());
        Assert.Equal("A lantern-lit alley in the rain", payload.GetProperty("prompt").GetString());
        Assert.Equal("2:3", payload.GetProperty("aspect_ratio").GetString());
        Assert.False(payload.TryGetProperty("input_references", out _));
        Assert.False(payload.TryGetProperty("messages", out _));
    }

    [Fact]
    public async Task EditImageAsync_SendsInputReferencesToImagesEndpoint()
    {
        var pngBytes = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02
        };
        var generatedBytes = Encoding.ASCII.GetBytes("edited-image");
        var handler = new QueueingHttpMessageHandler(
        [
            CreateJsonResponse(
                """
                {
                  "data": [
                    {
                      "id": "google/gemini-3.1-flash-image",
                      "name": "Gemini Image",
                      "architecture": {
                        "input_modalities": ["text", "image"],
                        "output_modalities": ["image"]
                      },
                      "supported_parameters": {
                        "input_references": {"type": "range", "min": 0, "max": 14}
                      }
                    }
                  ]
                }
                """),
            CreateJsonResponse(
                $$"""
                {
                  "data": [{"b64_json": "{{Convert.ToBase64String(generatedBytes)}}"}]
                }
                """)
        ]);
        var service = CreateService(handler);

        var result = await service.EditImageAsync(
            pngBytes,
            new ImageGenerationRequestDto
            {
                ModelId = "google/gemini-3.1-flash-image",
                Prompt = "Turn this sketch into a painted portrait",
                Width = 1024,
                Height = 1024
            });

        Assert.Equal(generatedBytes, result);

        var request = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post);
        Assert.Equal("/api/v1/images", request.RequestUri?.PathAndQuery);
        using var requestJson = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());

        var payload = requestJson.RootElement;
        Assert.Equal(
            "Turn this sketch into a painted portrait",
            payload.GetProperty("prompt").GetString());
        Assert.False(payload.TryGetProperty("aspect_ratio", out _));
        var reference = Assert.Single(payload.GetProperty("input_references").EnumerateArray());
        Assert.Equal("image_url", reference.GetProperty("type").GetString());
        Assert.Equal(
            $"data:image/png;base64,{Convert.ToBase64String(pngBytes)}",
            reference.GetProperty("image_url").GetProperty("url").GetString());
    }

    [Fact]
    public async Task EditImageAsync_ThrowsBadRequest_WhenModelDoesNotSupportImageInput()
    {
        var handler = new QueueingHttpMessageHandler(
        [
            CreateJsonResponse(
                """
                {
                  "data": [
                    {
                      "id": "black-forest-labs/flux.2-pro",
                      "name": "Flux Pro",
                      "architecture": {
                        "input_modalities": ["text"],
                        "output_modalities": ["image"]
                      }
                    }
                  ]
                }
                """)
        ]);
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<ApiException>(() => service.EditImageAsync(
            [0xFF, 0xD8, 0xFF],
            new ImageGenerationRequestDto
            {
                ModelId = "black-forest-labs/flux.2-pro",
                Prompt = "Add dramatic lighting",
                Width = 832,
                Height = 1248
            }));

        Assert.Equal(ErrorCodes.BadRequest, exception.Code);
        Assert.Contains("does not support image editing", exception.Message);
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post);
    }

    private static OpenRouterImageGenerationService CreateService(QueueingHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/")
        };

        return new OpenRouterImageGenerationService(
            httpClient,
            NullLogger<OpenRouterImageGenerationService>.Instance,
            new FakeIntegrationsService(new IntegrationsConfig
            {
                OpenRouterApiKey = "test-openrouter-key"
            }));
    }

    private static HttpResponseMessage CreateJsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class FakeIntegrationsService(IntegrationsConfig config) : IIntegrationsService
    {
        public ValueTask<IntegrationsConfig> GetConfigAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(config);
        }

        public Task UpdateConfigAsync(IntegrationsConfig config, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class QueueingHttpMessageHandler(IEnumerable<HttpResponseMessage> responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(CloneRequest(request));

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No queued response was available for the request.");
            }

            return Task.FromResult(_responses.Dequeue());
        }

        private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri);

            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (request.Content is null)
            {
                return clone;
            }

            var contentBytes = request.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            clone.Content = new ByteArrayContent(contentBytes);

            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }
    }
}
