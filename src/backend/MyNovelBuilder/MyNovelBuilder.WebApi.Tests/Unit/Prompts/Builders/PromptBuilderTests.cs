using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.Novels;
using MyNovelBuilder.WebApi.Prompts.Builders;

namespace MyNovelBuilder.WebApi.Tests.Unit.Prompts.Builders;

public class PromptBuilderTests
{
    private class TestPromptBuilder(string prompt) : PromptBuilder<TextGenerationContextInfoDto>(prompt)
    {
        public new static HashSet<CompendiumRecord> FilterRecordsInContext(
            IList<CompendiumRecord> records, string context)
        {
            return PromptBuilder<TextGenerationContextInfoDto>
                .FilterRecordsInContext(records, context);
        }

        public static string CreateCompendiumRecordsString(
            IList<CompendiumRecord> records, Prose prose, int? chapterIndex, int? sectionIndex)
        {
            return PromptBuilder<TextGenerationContextInfoDto>
                .CreateCompendiumRecordsString(
                    records, (prose, chapterIndex, sectionIndex));
        }
    }
    
    private class TestTextGenerationContextInfoDto : NovelTextGenerationContextInfoDto
    {
        
    }

    [Theory]
    [InlineData("Vera", "Vera was there.", true)]
    [InlineData("Vera", "average", false)]
    [InlineData("Vera", "Vera-like", false)]
    [InlineData("summer", "mid-summer", false)]
    [InlineData("Vera", "Vera.", true)]
    [InlineData("Vera", "Vera, she said", true)]
    [InlineData("Vera", "Is it Vera?", true)]
    [InlineData("Vera", "2Vera", false)]
    [InlineData("Vera", "Vera123", false)]
    public void IsRecordInContext_IdentifiesCorrectRecords(string name, string context, bool expected)
    {
        var record = new CompendiumRecord { Name = name, Aliases = "", Type = CompendiumRecordType.Character };
        var records = new List<CompendiumRecord> { record };
        
        var result = TestPromptBuilder.FilterRecordsInContext(records, context);
        
        if (expected) Assert.Contains(record, result);
        else Assert.DoesNotContain(record, result);
    }

    [Fact]
    public void IsRecordInContext_WorksWithAliases()
    {
        var record = new CompendiumRecord { Name = "Vera", Aliases = "V, Vee", Type = CompendiumRecordType.Character };
        var records = new List<CompendiumRecord> { record };
        
        var result1 = TestPromptBuilder.FilterRecordsInContext(records, "V was there.");
        var result2 = TestPromptBuilder.FilterRecordsInContext(records, "Vee was there.");
        
        Assert.Contains(record, result1);
        Assert.Contains(record, result2);
    }

    [Fact]
    public void FilterRecordsInContext_RecursiveDiscovery()
    {
        var recordB = new CompendiumRecord
        {
            Name = "RecordB",
            Aliases = "",
            Context = "Nothing here",
            Type = CompendiumRecordType.Character
        };
        var recordA = new CompendiumRecord
        {
            Name = "RecordA",
            Aliases = "",
            Context = "Mentioning RecordB here.",
            Type = CompendiumRecordType.Character
        };
        var records = new List<CompendiumRecord> { recordA, recordB };
        
        const string context = "Start with RecordA.";
        var result = TestPromptBuilder.FilterRecordsInContext(records, context);
        
        Assert.Contains(recordA, result);
        Assert.Contains(recordB, result);
    }

    [Fact]
    public void FilterRecordsInContext_IncludesAlwaysIncludedRecords()
    {
        var record = new CompendiumRecord
        {
            Name = "World",
            Aliases = "",
            AlwaysIncluded = true,
            Type = CompendiumRecordType.Place
        };
        var records = new List<CompendiumRecord> { record };
        
        var result = TestPromptBuilder.FilterRecordsInContext(records, "Some context that doesn't mention it.");
        
        Assert.Contains(record, result);
    }

    [Fact]
    public void ApplyContextOverrides_ReplacesKeywordBlocks()
    {
        var record = new CompendiumRecord 
        { 
            Name = "Vera", 
            Context = "Vera is [status]single[/status].",
            Type = CompendiumRecordType.Character,
            Id = Guid.NewGuid()
        };
        
        var prose = new Prose
        {
            Chapters = new List<Chapter>
            {
                new()
                {
                    Title = "Chapter 1",
                    Sections = new List<Section>
                    {
                        new()
                        {
                            RecordOverrides =
                            [
                                new RecordOverride
                                {
                                    CompendiumRecordId = record.Id,
                                    Keyword = "status",
                                    Description = "married"
                                }
                            ]
                        }
                    }
                }
            }
        };

        var result = TestPromptBuilder.CreateCompendiumRecordsString([record], prose, null, null);
        
        Assert.Contains("Vera is married.", result);
        Assert.DoesNotContain("single", result);
    }

    [Theory]
    [InlineData(WritingPov.FirstPerson, "The novel is written in first person")]
    [InlineData(WritingPov.ThirdPersonLimited, "The novel is written in third person (limited perspective)")]
    [InlineData(WritingPov.ThirdPersonOmniscient, "The novel is written in third person (omniscient)")]
    public void ReplacePlaceholders_GeneratesCorrectPovString(WritingPov pov, string expectedPart)
    {
        var novel = new Novel
        {
            Id = Guid.NewGuid(),
            Title = "Test", 
            Pov = pov, 
            Language = WritingLanguage.English,
            Tense = WritingTense.Past
        };
        
        var context = new PromptBuilderContext<TextGenerationContextInfoDto>
        {
            Client = new TestTextGenerationContextInfoDto
            {
                NovelId = novel.Id
            },
            Novel = novel,
            Prose = new Prose(),
            CompendiumRecords = new List<CompendiumRecord>(),
            IncludedCompendiumRecordIds = new HashSet<Guid>()
        };
        
        var builder = new TestPromptBuilder("{{novel.pov}}");
        builder.ReplacePlaceholders(context);
        
        Assert.Contains(expectedPart, builder.ToString());
    }

    [Fact]
    public void ReplacePlaceholders_IncludesMainCharacterInPov()
    {
        var novel = new Novel 
        { 
            Id = Guid.NewGuid(),
            Title = "Test",
            Pov = WritingPov.ThirdPersonLimited, 
            Language = WritingLanguage.English, 
            Tense = WritingTense.Past,
            MainCharacter = new CompendiumRecord
            {
                Name = "John",
                Type = CompendiumRecordType.Character
            }
        };
        var context = new PromptBuilderContext<TextGenerationContextInfoDto>
        {
            Client = new TestTextGenerationContextInfoDto
            {
                NovelId = novel.Id
            },
            Novel = novel,
            Prose = new Prose(),
            CompendiumRecords = new List<CompendiumRecord>(),
            IncludedCompendiumRecordIds = new HashSet<Guid>()
        };
        
        var builder = new TestPromptBuilder("{{novel.pov}}");
        builder.ReplacePlaceholders(context);
        
        Assert.Contains("from the perspective of John", builder.ToString());
    }

    [Fact]
    public void ReplacePlaceholders_GeneratesCorrectTenseString()
    {
        var novel = new Novel
        {
            Id = Guid.NewGuid(),
            Title = "Test",
            Tense = WritingTense.Past,
            Language = WritingLanguage.English
        };
        var context = new PromptBuilderContext<TextGenerationContextInfoDto>
        {
            Client = new TestTextGenerationContextInfoDto
            {
                NovelId = novel.Id
            },
            Novel = novel,
            Prose = new Prose(),
            CompendiumRecords = new List<CompendiumRecord>(),
            IncludedCompendiumRecordIds = new HashSet<Guid>()
        };
        
        var builder = new TestPromptBuilder("{{novel.tense}}");
        builder.ReplacePlaceholders(context);
        
        Assert.Equal("Past tense", builder.ToString());
    }
}
