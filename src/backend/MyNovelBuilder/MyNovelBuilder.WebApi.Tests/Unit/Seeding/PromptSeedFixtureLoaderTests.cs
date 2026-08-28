using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Seeding;

namespace MyNovelBuilder.WebApi.Tests.Unit.Seeding;

public sealed class PromptSeedFixtureLoaderTests : IDisposable
{
    private readonly string testRoot = Path.Combine(
        Path.GetTempPath(),
        $"mynovelbuilder-seed-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task LoadBundledAsync_ValidatesShippedPromptSet()
    {
        var fixture = await PromptSeedFixtureLoader.LoadBundledAsync();

        Assert.Equal(15, fixture.Prompts.Count);
        Assert.All(fixture.Prompts, prompt => Assert.Equal(1, prompt.SeedVersion));
        Assert.Equal(
            fixture.Prompts.Count,
            fixture.Prompts.Select(prompt => prompt.SeedKey).Distinct().Count());
    }

    [Fact]
    public async Task LoadBundledAsync_EachPromptUsesEveryAvailableKeyword()
    {
        var fixture = await PromptSeedFixtureLoader.LoadBundledAsync();
        var keywordsByType = new Dictionary<PromptType, string[]>
        {
            [PromptType.GenerateText] =
                ["{{context}}", "{{instructions}}", "{{records}}"],
            [PromptType.SummarizeText] = ["{{context}}", "{{records}}"],
            [PromptType.ReplaceText] =
            [
                "{{textBefore}}",
                "{{textAfter}}",
                "{{instructions}}",
                "{{textToReplace}}",
                "{{records}}"
            ],
            [PromptType.CreateCompendiumRecord] =
                ["{{context}}", "{{instructions}}", "{{recordDetails}}", "{{records}}"],
            [PromptType.SendChatMessage] =
                ["{{context}}", "{{chatHistory}}", "{{instructions}}", "{{records}}"],
            [PromptType.DescribeCompendiumImage] =
                ["{{instructions}}", "{{records}}"],
            [PromptType.CreateCompendiumRecordImageGenerationPrompt] =
                ["{{record}}", "{{instructions}}", "{{records}}"],
            [PromptType.CreateStoryEvents] =
            [
                "{{context}}",
                "{{previousChapterEvents}}",
                "{{nextChapterEvents}}",
                "{{records}}"
            ],
            [PromptType.TranslateNovel] =
                ["{{targetLanguage}}", "{{context}}", "{{instructions}}", "{{records}}"],
            [PromptType.DescribeImage] = ["{{instructions}}"],
            [PromptType.PrepareImmersiveTts] =
            [
                "{{chapterTitle}}",
                "{{sectionSummary}}",
                "{{sectionText}}",
                "{{storySoFar}}",
                "{{wholeChapter}}",
                "{{records}}",
                "{{speakerOptions}}"
            ],
            [PromptType.SuggestStoryDevelopments] =
            [
                "{{context}}",
                "{{currentChapterTitle}}",
                "{{currentChapterEvents}}",
                "{{sectionSummary}}",
                "{{records}}"
            ],
            [PromptType.WorldBuildingAgent] =
            [
                "{{premise}}",
                "{{novel}}",
                "{{prose}}",
                "{{compendia}}",
                "{{records}}",
                "{{proposalHistory}}",
                "{{chatHistory}}",
                "{{instructions}}"
            ]
        };
        var novelPromptTypes = new HashSet<PromptType>
        {
            PromptType.GenerateText,
            PromptType.SummarizeText,
            PromptType.ReplaceText,
            PromptType.CreateCompendiumRecord,
            PromptType.SendChatMessage,
            PromptType.CreateStoryEvents,
            PromptType.TranslateNovel,
            PromptType.PrepareImmersiveTts,
            PromptType.SuggestStoryDevelopments
        };
        string[] novelKeywords =
            ["{{novel.language}}", "{{novel.pov}}", "{{novel.tense}}"];

        foreach (var prompt in fixture.Prompts)
        {
            var promptText = string.Join("\n", prompt.Messages.Select(message => message.Message));
            var expectedKeywords = keywordsByType[prompt.Type];
            if (novelPromptTypes.Contains(prompt.Type))
            {
                expectedKeywords = expectedKeywords.Concat(novelKeywords).ToArray();
            }

            foreach (var keyword in expectedKeywords)
            {
                Assert.Contains(keyword, promptText);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_ParsesValidFixture()
    {
        var fixturePath = WriteFixture(
            """
            {
              "schemaVersion": 1,
              "prompts": [
                {
                  "seedKey": "generate-prose",
                  "seedVersion": 2,
                  "name": "Generate prose",
                  "type": "generateText",
                  "messages": [
                    {
                      "role": "system",
                      "message": "Write vivid prose."
                    }
                  ]
                }
              ]
            }
            """);

        var fixture = await PromptSeedFixtureLoader.LoadAsync(fixturePath);

        Assert.Equal(1, fixture.SchemaVersion);
        var prompt = Assert.Single(fixture.Prompts);
        Assert.Equal("generate-prose", prompt.SeedKey);
        Assert.Equal(2, prompt.SeedVersion);
        Assert.Equal(PromptType.GenerateText, prompt.Type);
        Assert.Equal(PromptMessageRole.System, Assert.Single(prompt.Messages).Role);
    }

    [Fact]
    public async Task LoadAsync_RejectsUnsupportedSchemaVersion()
    {
        var fixturePath = WriteFixture(
            """
            {
              "schemaVersion": 99,
              "prompts": []
            }
            """);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => PromptSeedFixtureLoader.LoadAsync(fixturePath));

        Assert.Contains("schemaVersion must be 1", exception.Message);
    }

    [Fact]
    public async Task LoadAsync_RejectsDuplicateSeedKeys()
    {
        var fixturePath = WriteFixture(
            """
            {
              "schemaVersion": 1,
              "prompts": [
                {
                  "seedKey": "duplicate",
                  "seedVersion": 1,
                  "name": "First",
                  "type": "generateText",
                  "messages": [{ "role": "user", "message": "First" }]
                },
                {
                  "seedKey": "duplicate",
                  "seedVersion": 1,
                  "name": "Second",
                  "type": "summarizeText",
                  "messages": [{ "role": "user", "message": "Second" }]
                }
              ]
            }
            """);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => PromptSeedFixtureLoader.LoadAsync(fixturePath));

        Assert.Contains("seedKey 'duplicate' is duplicated", exception.Message);
    }

    [Theory]
    [InlineData("\"messages\": []", "messages must contain at least one message")]
    [InlineData("\"seedVersion\": 0", "seedVersion must be at least 1")]
    [InlineData("\"name\": \"\"", "name must contain between 1 and 100 characters")]
    public async Task LoadAsync_RejectsInvalidPromptValues(
        string replacement,
        string expectedMessage)
    {
        const string validFixture =
            """
            {
              "schemaVersion": 1,
              "prompts": [
                {
                  "seedKey": "prompt",
                  "seedVersion": 1,
                  "name": "Prompt",
                  "type": "generateText",
                  "messages": [{ "role": "user", "message": "Write" }]
                }
              ]
            }
            """;
        var originalProperty = replacement.Split(':', 2)[0] switch
        {
            "\"messages\"" => "\"messages\": [{ \"role\": \"user\", \"message\": \"Write\" }]",
            "\"seedVersion\"" => "\"seedVersion\": 1",
            "\"name\"" => "\"name\": \"Prompt\"",
            _ => throw new InvalidOperationException("Unknown test property.")
        };
        var fixturePath = WriteFixture(validFixture.Replace(originalProperty, replacement));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => PromptSeedFixtureLoader.LoadAsync(fixturePath));

        Assert.Contains(expectedMessage, exception.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private string WriteFixture(string contents)
    {
        Directory.CreateDirectory(testRoot);
        var fixturePath = Path.Combine(testRoot, $"{Guid.NewGuid():N}.json");
        File.WriteAllText(fixturePath, contents);
        return fixturePath;
    }
}
