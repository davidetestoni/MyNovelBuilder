using System.Text;
using System.Text.RegularExpressions;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Extensions;
using MyNovelBuilder.WebApi.Models.Novels;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// A builder for prompts.
/// </summary>
/// <typeparam name="T"></typeparam>
public partial class PromptBuilder<T> where T : TextGenerationContextInfoDto
{
    /// <summary>
    /// The backing string builder.
    /// </summary>
    protected readonly StringBuilder Builder;

    /// <summary>
    /// Creates a new prompt builder.
    /// </summary>
    protected PromptBuilder(string prompt)
    {
        Builder = new StringBuilder(prompt);
    }
    
    /// <summary>
    /// Replaces placeholders in the prompt with information from the context. 
    /// </summary>
    public virtual PromptBuilder<T> ReplacePlaceholders(PromptBuilderContext<T> context)
    {
        Builder
            .Replace("{{novel.language}}", context.Novel.Language.ToString())
            .Replace("{{novel.pov}}", CreateNovelPovString(context.Novel))
            .Replace("{{novel.tense}}", $"{context.Novel.Tense} tense");
        
        return this;
    }
    
    /// <inheritdoc />
    public override string ToString()
    {
        return Builder.ToString();
    }
    
    private static string CreateNovelPovString(Novel novel)
    {
        var povStringBuilder = new StringBuilder("The novel is written in ");
        
        povStringBuilder.Append(novel.Pov switch
        {
            WritingPov.FirstPerson => "first person",
            WritingPov.ThirdPersonLimited => "third person (limited perspective)",
            WritingPov.ThirdPersonOmniscient => "third person (omniscient)",
            _ => throw new NotImplementedException("Unknown POV type.")
        });
        
        if (novel.MainCharacter is not null)
        {
            povStringBuilder.Append(" from the perspective of ");
            povStringBuilder.Append(novel.MainCharacter.Name);
            povStringBuilder.Append('.');
        }
        
        return povStringBuilder.ToString();
    }
    
    /// <summary>
    /// Creates a string representation of the compendium records,
    /// while applying any context overrides up to the specified
    /// chapter and section (all if null).
    /// Only provide the records that are in context.
    /// </summary>
    protected static string CreateCompendiumRecordsString(
        IList<CompendiumRecord> records,
        Prose prose,
        int? chapterIndex,
        int? sectionIndex)
    {
        var recordsBuilder = new StringBuilder();
        
        foreach (var record in records)
        {
            recordsBuilder.Append(record.Name);
            recordsBuilder.Append(" (");
            recordsBuilder.Append(record.Type);
            recordsBuilder.Append(")\n");
            recordsBuilder.Append(
                ApplyContextOverrides(record, prose, chapterIndex, sectionIndex));
            recordsBuilder.Append("\n\n");
        }
        
        return recordsBuilder.ToString();
    }

    /// <summary>
    /// Applies any context overrides to the compendium record,
    /// up to the specified chapter and section (all if null),
    /// returning the modified description.
    /// </summary>
    private static string ApplyContextOverrides(
        CompendiumRecord record, Prose prose,
        int? chapterIndex, int? sectionIndex)
    {
        var recordContext = record.Context;
        var recordOverrides = new List<RecordOverride>();
        
        var endChapterIndex = chapterIndex ?? prose.Chapters.Count - 1;
        var endSectionIndex = sectionIndex ?? prose.Chapters[endChapterIndex].Sections.Count - 1;

        for (var c = 0; c <= endChapterIndex; c++)
        {
            for (var s = 0; s < prose.Chapters[c].Sections.Count; s++)
            {
                // If we are past the target chapter and section, stop
                if (c == chapterIndex && s > endSectionIndex)
                {
                    break;
                }

                // Find any overrides for the record in this section
                var overrides = prose.Chapters[c].Sections[s].RecordOverrides
                    .Where(ro => ro.CompendiumRecordId == record.Id)
                    .ToList();

                recordOverrides.AddRange(overrides);
            }
        }
        
        // Apply the overrides in order
        foreach (var recordOverride in recordOverrides)
        {
            if (string.IsNullOrWhiteSpace(recordOverride.Keyword))
            {
                continue;
            }
            
            // Context regions are in the format:
            // [keyword]...[/keyword]
            var keywordRegionPattern = $@"\[{Regex.Escape(recordOverride.Keyword)}\](?:.|\n)*?\[\/{Regex.Escape(recordOverride.Keyword)}\]";
            var regex = new Regex(keywordRegionPattern);
            recordContext = regex.Replace(recordContext, recordOverride.Description);
        }
        
        return recordContext;
    }

    /// <summary>
    /// Filters the records based on the context.
    /// </summary>
    protected static HashSet<CompendiumRecord> FilterRecordsInContext(IList<CompendiumRecord> records, string context)
    {
        // Index records by their name and any aliases
        var processedRecords = new HashSet<CompendiumRecord>(); // To avoid infinite loops
        var recordsInContext = new HashSet<CompendiumRecord>();

        foreach (var record in records)
        {
            // If the record is always included, we don't need to check if it's in context,
            // so we just add it to the records in context
            if (record.AlwaysIncluded)
            {
                ProcessRecordInContext(record, records, processedRecords, recordsInContext);
                continue;
            }
            
            // If the record is in context, add it to the records in context
            if (IsRecordInContext(record, context))
            {
                ProcessRecordInContext(record, records, processedRecords, recordsInContext);
            }
        }
        
        return recordsInContext;
    }

    /// <summary>
    /// Adds the records mentioned in the given record's context to the records in context.
    /// </summary>
    /// <param name="record">The record to search for.</param>
    /// <param name="records">The list of records.</param>
    /// <param name="processedRecords">The records that have already been processed.</param>
    /// <param name="recordsInContext">The records in context.</param>
    private static void ProcessRecordInContext(
        CompendiumRecord record, IList<CompendiumRecord> records,
        HashSet<CompendiumRecord> processedRecords, HashSet<CompendiumRecord> recordsInContext)
    {
        // Add the record to the processed records.
        // If it has already been processed, return (exit condition)
        if (!processedRecords.Add(record))
        {
            return;
        }
        
        // Add the record to the records in context
        recordsInContext.Add(record);
        
        // Recursively search for unprocessed records in context and process them
        foreach (var subRecord in records.Except(processedRecords).Where(r => IsRecordInContext(r, record.Context)))
        {
            ProcessRecordInContext(subRecord, records, processedRecords, recordsInContext);
        }
    }

    private static bool IsRecordInContext(CompendiumRecord record, string context)
    {
        string[] aliases = [
            record.Name, 
            ..record.Aliases.Split(
                ',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        ];
        
        foreach (var alias in aliases)
        {
            // Search for all occurrences of the alias in the context and get their indices
            var indices = context.AllIndexesOf(alias);

            foreach (var index in indices)
            {
                // If the characters around the alias are word separators, the alias is in context
                if ((index == 0 || IsWordSeparator(context[index - 1])) && 
                    (index + alias.Length == context.Length || IsWordSeparator(context[index + alias.Length])))
                {
                    return true;
                }
            }
        }

        return false;
    }
    
    /// <summary>
    /// Checks if the character is a word separator, which means that
    /// the character is not part of a word.
    /// </summary>
    /// <param name="c"></param>
    /// <returns></returns>
    private static bool IsWordSeparator(char c)
    {
        return
            !char.IsLetter(c) && // i.e., do not trigger on "average" for "vera"
            !char.IsDigit(c) && // i.e., do not trigger on "2pac" for "pac"
            c != '-'; // i.e., do not trigger on "mid-summer" for "summer"
    }
    
    /// <summary>
    /// Strips HTML tags from the text and decodes HTML entities.
    /// </summary>
    protected static string StripHtmlTags(string text)
    {
        return StripHtmlTagsRegex().Replace(text, string.Empty);
    }
    
    /// <summary>
    /// Decodes HTML entities in the text.
    /// </summary>
    protected static string DecodeHtmlEntities(string text)
    {
        return System.Net.WebUtility.HtmlDecode(text);
    }
    
    /// <summary>
    /// Gets the chapter at the specified index.
    /// </summary>
    /// <exception cref="IndexOutOfRangeException">
    /// When the chapter index is out of bounds.
    /// </exception>
    protected static Chapter GetChapter(Prose prose, int chapterIndex)
    {
        // Check if the chapter index is out of bounds
        if (chapterIndex >= prose.Chapters.Count)
        {
            throw new IndexOutOfRangeException(
                "The chapter index is out of bounds.");
        }
        
        return prose.Chapters[chapterIndex];
    }
    
    /// <summary>
    /// Gets the section at the specified index.
    /// Returns null if there are no sections in the chapter yet.
    /// </summary>
    /// <exception cref="IndexOutOfRangeException">
    /// When the section index is out of bounds.
    /// </exception>
    protected static Section? GetSection(Chapter chapter, int sectionIndex)
    {
        // If there are no sections yet in this chapter,
        // return null.
        if (chapter.Sections.Count == 0)
        {
            return null;
        }
        
        // Check if the section index is out of bounds
        if (sectionIndex >= chapter.Sections.Count)
        {
            throw new IndexOutOfRangeException(
                "The section index is out of bounds.");
        }
        
        return chapter.Sections[sectionIndex];
    }

    /// <summary>
    /// Gets the entire story as plain text.
    /// </summary>
    protected static string GetEntireStory(Prose prose)
    {
        var storyBuilder = new StringBuilder();
        
        foreach (var chapter in prose.Chapters)
        {
            storyBuilder.AppendLine($"# {StripHtmlTags(chapter.Title)}\n\n");
            
            foreach (var section in chapter.Sections)
            {
                storyBuilder.Append(StripHtmlTags(section.Text));
                storyBuilder.Append("\n\n");
            }
        }
        
        return storyBuilder.ToString();
    }

    /// <summary>
    /// Gets the whole chapter at the specified index.
    /// </summary>
    protected static string GetWholeChapter(Prose prose, int chapterIndex)
    {
        var chapter = GetChapter(prose, chapterIndex);
        var chapterBuilder = new StringBuilder();
        
        foreach (var section in chapter.Sections)
        {
            chapterBuilder.Append(StripHtmlTags(section.Text));
            chapterBuilder.Append("\n\n");
        }
        
        return chapterBuilder.ToString();
    }

    /// <summary>
    /// Gets the story so far up to the specified offset in the text.
    /// </summary>
    protected static string GetStorySoFar(
        Prose prose, int chapterIndex, int sectionIndex, int textOffset)
    {
        var chapter = GetChapter(prose, chapterIndex);
        var section = GetSection(chapter, sectionIndex);
        var text = StripHtmlTags(section?.Text ?? string.Empty);
        
        List<Section> previousSections;
        
        // Get up to 6 previous sections (even across chapters)
        if (section is null)
        {
            // If the section is null, this means that there
            // are no sections in the chapter yet, so we return
            // up to the last section in the previous chapter.
            previousSections = prose.Chapters
                .TakeWhile(c => c != chapter)
                .SelectMany(c => c.Sections)
                .TakeLast(6)
                .ToList();
        }
        else
        {
            previousSections = prose.Chapters
                .SelectMany(c => c.Sections)
                .TakeWhile(s => s != section)
                .TakeLast(6)
                .ToList();
        }

        var contextBuilder = new StringBuilder();
        
        // Append the summaries of the previous sections except the last one (up to 5)
        foreach (var previousSection in previousSections)
        {
            contextBuilder.Append(StripHtmlTags(previousSection.Text));
            contextBuilder.Append("\n\n");
        }
        
        // Append the text of the last previous section (if any)
        if (previousSections.Count != 0)
        {
            contextBuilder.Append(StripHtmlTags(previousSections[^1].Text));
            contextBuilder.Append("\n\n");
        }
        
        // Append the text of the current section, up to the offset
        contextBuilder.Append(text[..textOffset]);

        return contextBuilder.ToString();
    }

    [GeneratedRegex("<.*?>")]
    private static partial Regex StripHtmlTagsRegex();
}
