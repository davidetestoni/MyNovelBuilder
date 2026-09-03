using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Seeding;

internal sealed class PromptSeedFixture
{
    public required int SchemaVersion { get; init; }

    public required IReadOnlyList<PromptSeedDefinition> Prompts { get; init; }
}

internal sealed class PromptSeedDefinition
{
    public required string SeedKey { get; init; }

    public required int SeedVersion { get; init; }

    public required string Name { get; init; }

    public required PromptType Type { get; init; }

    public required IReadOnlyList<PromptSeedMessage> Messages { get; init; }
}

internal sealed class PromptSeedMessage
{
    public required PromptMessageRole Role { get; init; }

    public required string Message { get; init; }
}
