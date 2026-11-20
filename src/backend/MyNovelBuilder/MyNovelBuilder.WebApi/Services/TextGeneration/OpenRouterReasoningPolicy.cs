using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json.Nodes;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Services.TextGeneration;

// The pinned OpenAI SDK predates reasoning controls. Add the field after SDK
// serialization so regular, streamed, structured and vision calls behave alike.
internal sealed class OpenRouterReasoningPolicy(ReasoningEffort effort) : PipelinePolicy
{
    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        using var buffer = new MemoryStream();
        message.Request.Content?.WriteTo(buffer, message.CancellationToken);
        Apply(message, buffer);
        ProcessNext(message, pipeline, currentIndex);
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        using var buffer = new MemoryStream();
        if (message.Request.Content is { } content)
            await content.WriteToAsync(buffer, message.CancellationToken);
        Apply(message, buffer);
        await ProcessNextAsync(message, pipeline, currentIndex);
    }

    private void Apply(PipelineMessage message, MemoryStream buffer)
    {
        if (buffer.Length == 0) return;
        var body = JsonNode.Parse(buffer.ToArray())!.AsObject();
        body["reasoning"] = effort == ReasoningEffort.None
            ? new JsonObject { ["enabled"] = false, ["effort"] = "none" }
            : new JsonObject { ["enabled"] = true, ["effort"] = effort.ToString().ToLowerInvariant() };
        message.Request.Content = BinaryContent.Create(BinaryData.FromString(body.ToJsonString()));
    }
}
