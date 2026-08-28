using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Seeding;

namespace MyNovelBuilder.WebApi.Tests.Unit.Seeding;

public sealed class SampleNovelSeedFixtureLoaderTests : IDisposable
{
    private readonly string testRoot = Path.Combine(
        Path.GetTempPath(),
        $"mynovelbuilder-sample-seed-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task LoadBundledAsync_ValidatesRepresentativeSampleNovel()
    {
        var fixture = await SampleNovelSeedFixtureLoader.LoadBundledAsync();

        Assert.Equal(1, fixture.Manifest.SchemaVersion);
        Assert.Contains("Sample Novel", fixture.Manifest.Title);
        Assert.True(fixture.Manifest.RpgMode);
        Assert.Equal(3, fixture.Compendia.Count);

        var records = fixture.Compendia.SelectMany(compendium => compendium.Records).ToList();
        Assert.Equal(10, records.Count);
        Assert.Equal(
            Enum.GetValues<CompendiumRecordType>().Order(),
            records.Select(record => record.Type).Distinct().Order());

        var mainCharacter = Assert.Single(
            records,
            record => record.SeedKey == fixture.Manifest.MainCharacterKey);
        Assert.Equal(CompendiumRecordType.Character, mainCharacter.Type);

        Assert.Equal(3, fixture.Prose.Chapters.Count);
        Assert.Equal(6, fixture.Prose.Chapters.Sum(chapter => chapter.Sections.Count));
        Assert.Equal(6, fixture.Prose.Chapters.Sum(chapter => chapter.StoryEvents.Count));
        Assert.Equal(
            5,
            fixture.Prose.Chapters
                .SelectMany(chapter => chapter.Sections)
                .Sum(section => section.RecordOverrides.Count));
    }

    [Fact]
    public async Task LoadAsync_RejectsDuplicateRecordKeysAcrossFiles()
    {
        var root = WriteValidFixture();
        var compendiumPath = Path.Combine(root, "compendia.json");
        var contents = (await File.ReadAllTextAsync(compendiumPath))
            .Replace("\"second-record\"", "\"main-character\"");
        await File.WriteAllTextAsync(compendiumPath, contents);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => SampleNovelSeedFixtureLoader.LoadAsync(root));

        Assert.Contains("seedKey 'main-character' is duplicated", exception.Message);
    }

    [Fact]
    public async Task LoadAsync_RejectsUnknownOverrideRecordKey()
    {
        var root = WriteValidFixture();
        var prosePath = Path.Combine(root, "prose.json");
        var contents = (await File.ReadAllTextAsync(prosePath))
            .Replace("\"main-character\"", "\"missing-record\"");
        await File.WriteAllTextAsync(prosePath, contents);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => SampleNovelSeedFixtureLoader.LoadAsync(root));

        Assert.Contains("override for an unknown recordKey", exception.Message);
    }

    [Fact]
    public async Task LoadAsync_RejectsFilesOutsideFixtureDirectory()
    {
        var root = WriteValidFixture();
        var manifestPath = Path.Combine(root, "manifest.json");
        var contents = (await File.ReadAllTextAsync(manifestPath))
            .Replace("\"prose.json\"", "\"../prose.json\"");
        await File.WriteAllTextAsync(manifestPath, contents);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => SampleNovelSeedFixtureLoader.LoadAsync(root));

        Assert.Contains("leaves the fixture directory", exception.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private string WriteValidFixture()
    {
        var root = Path.Combine(testRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        File.WriteAllText(
            Path.Combine(root, "manifest.json"),
            """
            {
              "schemaVersion": 1,
              "title": "Sample",
              "author": "Author",
              "brief": "A fixture",
              "tense": "past",
              "pov": "thirdPersonLimited",
              "language": "english",
              "rpgMode": true,
              "mainCharacterKey": "main-character",
              "proseFile": "prose.json",
              "compendiumFiles": ["compendia.json"]
            }
            """);
        File.WriteAllText(
            Path.Combine(root, "compendia.json"),
            """
            {
              "compendia": [
                {
                  "seedKey": "people",
                  "name": "People",
                  "description": "Characters",
                  "records": [
                    {
                      "seedKey": "main-character",
                      "name": "Main Character",
                      "aliases": "hero",
                      "type": "character",
                      "context": "The protagonist.",
                      "alwaysIncluded": true
                    },
                    {
                      "seedKey": "second-record",
                      "name": "Place",
                      "aliases": "home",
                      "type": "place",
                      "context": "A location.",
                      "alwaysIncluded": false
                    }
                  ]
                }
              ]
            }
            """);
        File.WriteAllText(
            Path.Combine(root, "prose.json"),
            """
            {
              "chapters": [
                {
                  "title": "Chapter One",
                  "storyEvents": [
                    { "title": "An event", "date": "Day one", "description": "Something changes." }
                  ],
                  "sections": [
                    {
                      "summary": "A summary.",
                      "text": "<p>Some prose.</p>",
                      "images": [],
                      "recordOverrides": [
                        {
                          "recordKey": "main-character",
                          "keyword": "goal",
                          "description": "Find an answer."
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """);

        return root;
    }
}
