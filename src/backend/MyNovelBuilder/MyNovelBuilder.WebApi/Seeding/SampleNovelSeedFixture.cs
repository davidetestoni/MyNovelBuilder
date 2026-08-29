using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Seeding;

internal sealed class SampleNovelSeedFixture
{
    public required string RootPath { get; init; }

    public required SampleNovelSeedManifest Manifest { get; init; }

    public required IReadOnlyList<SampleCompendiumSeedDefinition> Compendia { get; init; }

    public required SampleProseSeedDefinition Prose { get; init; }
}

internal sealed class SampleNovelSeedManifest
{
    public required int SchemaVersion { get; init; }

    public required string Title { get; init; }

    public required string Author { get; init; }

    public required string Brief { get; init; }

    public required WritingTense Tense { get; init; }

    public required WritingPov Pov { get; init; }

    public required WritingLanguage Language { get; init; }

    public required bool RpgMode { get; init; }

    public required string MainCharacterKey { get; init; }

    public required string ProseFile { get; init; }

    public required IReadOnlyList<string> CompendiumFiles { get; init; }

    public required IReadOnlyList<SampleNovelSeedAssetDefinition> Assets { get; init; }
}

internal sealed class SampleNovelSeedAssetDefinition
{
    public required string SeedKey { get; init; }

    public required string File { get; init; }

    public required string Sha256 { get; init; }

    public required SampleNovelSeedAssetKind Kind { get; init; }

    public string? RecordKey { get; init; }
}

internal enum SampleNovelSeedAssetKind
{
    Cover,
    RecordImage,
    ProseImage
}

internal sealed class SampleCompendiumSeedFile
{
    public required IReadOnlyList<SampleCompendiumSeedDefinition> Compendia { get; init; }
}

internal sealed class SampleCompendiumSeedDefinition
{
    public required string SeedKey { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required IReadOnlyList<SampleCompendiumRecordSeedDefinition> Records { get; init; }
}

internal sealed class SampleCompendiumRecordSeedDefinition
{
    public required string SeedKey { get; init; }

    public required string Name { get; init; }

    public required string Aliases { get; init; }

    public required CompendiumRecordType Type { get; init; }

    public required string Context { get; init; }

    public required bool AlwaysIncluded { get; init; }
}

internal sealed class SampleProseSeedDefinition
{
    public required IReadOnlyList<SampleChapterSeedDefinition> Chapters { get; init; }
}

internal sealed class SampleChapterSeedDefinition
{
    public required string Title { get; init; }

    public required IReadOnlyList<SampleSectionSeedDefinition> Sections { get; init; }

    public required IReadOnlyList<SampleStoryEventSeedDefinition> StoryEvents { get; init; }
}

internal sealed class SampleSectionSeedDefinition
{
    public required string Summary { get; init; }

    public required string Text { get; init; }

    public required IReadOnlyList<string> Images { get; init; }

    public required IReadOnlyList<SampleRecordOverrideSeedDefinition> RecordOverrides { get; init; }
}

internal sealed class SampleStoryEventSeedDefinition
{
    public required string Title { get; init; }

    public required string Date { get; init; }

    public required string Description { get; init; }
}

internal sealed class SampleRecordOverrideSeedDefinition
{
    public required string RecordKey { get; init; }

    public required string Keyword { get; init; }

    public required string Description { get; init; }
}
