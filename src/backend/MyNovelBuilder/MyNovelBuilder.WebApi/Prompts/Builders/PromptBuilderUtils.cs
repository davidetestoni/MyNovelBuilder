using System.Text;
using System.Text.RegularExpressions;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Extensions;
using MyNovelBuilder.WebApi.Models.Novels;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// Shared helper methods for prompt builders.
/// </summary>
public static class PromptBuilderUtils
{
    /// <summary>
    /// Creates a string describing the novel POV.
    /// </summary>
    public static string CreateNovelPovString(Novel novel)
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
    /// Creates a string representation of the compendium records.
    /// </summary>
    public static string CreateCompendiumRecordsString(
        IList<CompendiumRecord> records,
        (Prose prose, int? chapterIndex, int? sectionIndex)? proseInfo = null)
    {
        var recordsBuilder = new StringBuilder();

        foreach (var record in records)
        {
            recordsBuilder.Append(record.Name);
            recordsBuilder.Append(" (");
            recordsBuilder.Append(record.Type);
            recordsBuilder.Append(")\n");

            if (proseInfo.HasValue)
            {
                recordsBuilder.Append(
                    ApplyContextOverrides(
                        record,
                        proseInfo.Value.prose,
                        proseInfo.Value.chapterIndex,
                        proseInfo.Value.sectionIndex));
            }
            else
            {
                recordsBuilder.Append(record.Context);
            }

            recordsBuilder.Append("\n\n");
        }

        return recordsBuilder.ToString();
    }

    /// <summary>
    /// Tracks the IDs for the records included in the prompt.
    /// </summary>
    public static void TrackIncludedRecords(
        ISet<Guid> includedCompendiumRecordIds,
        IEnumerable<CompendiumRecord> records)
    {
        foreach (var record in records)
        {
            includedCompendiumRecordIds.Add(record.Id);
        }
    }

    /// <summary>
    /// Filters the records based on the context.
    /// </summary>
    public static HashSet<CompendiumRecord> FilterRecordsInContext(
        IList<CompendiumRecord> records,
        string context)
    {
        var processedRecords = new HashSet<CompendiumRecord>();
        var recordsInContext = new HashSet<CompendiumRecord>();

        foreach (var record in records)
        {
            if (record.AlwaysIncluded)
            {
                ProcessRecordInContext(record, records, processedRecords, recordsInContext);
                continue;
            }

            if (IsRecordInContext(record, context))
            {
                ProcessRecordInContext(record, records, processedRecords, recordsInContext);
            }
        }

        return recordsInContext;
    }

    /// <summary>
    /// Gets the chapter at the specified index.
    /// </summary>
    public static Chapter GetChapter(Prose prose, int chapterIndex)
    {
        if (chapterIndex >= prose.Chapters.Count)
        {
            throw new IndexOutOfRangeException("The chapter index is out of bounds.");
        }

        return prose.Chapters[chapterIndex];
    }

    /// <summary>
    /// Gets the section at the specified index.
    /// </summary>
    public static Section? GetSection(Chapter chapter, int sectionIndex)
    {
        if (chapter.Sections.Count == 0)
        {
            return null;
        }

        if (sectionIndex >= chapter.Sections.Count)
        {
            throw new IndexOutOfRangeException("The section index is out of bounds.");
        }

        return chapter.Sections[sectionIndex];
    }

    /// <summary>
    /// Gets the entire story as plain text.
    /// </summary>
    public static string GetEntireStory(Prose prose)
    {
        var storyBuilder = new StringBuilder();

        foreach (var chapter in prose.Chapters)
        {
            storyBuilder.AppendLine($"# {chapter.Title.StripHtml()}\n\n");

            foreach (var section in chapter.Sections)
            {
                storyBuilder.Append(section.Text.StripHtml());
                storyBuilder.Append("\n\n");
            }
        }

        return storyBuilder.ToString();
    }

    /// <summary>
    /// Gets the whole chapter at the specified index.
    /// </summary>
    public static string GetWholeChapter(Prose prose, int chapterIndex)
    {
        var chapter = GetChapter(prose, chapterIndex);
        var chapterBuilder = new StringBuilder();

        foreach (var section in chapter.Sections)
        {
            chapterBuilder.Append(section.Text.StripHtml());
            chapterBuilder.Append("\n\n");
        }

        return chapterBuilder.ToString();
    }

    /// <summary>
    /// Gets the story so far up to the specified offset in the text.
    /// </summary>
    public static string GetStorySoFar(
        Prose prose,
        int chapterIndex,
        int sectionIndex,
        int textOffset)
    {
        var chapter = GetChapter(prose, chapterIndex);
        var section = GetSection(chapter, sectionIndex);
        var text = section?.Text.StripHtml() ?? string.Empty;

        List<Section> previousSections;

        if (section is null)
        {
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

        foreach (var previousSection in previousSections)
        {
            contextBuilder.Append(previousSection.Text.StripHtml());
            contextBuilder.Append("\n\n");
        }

        if (previousSections.Count != 0)
        {
            contextBuilder.Append(previousSections[^1].Text.StripHtml());
            contextBuilder.Append("\n\n");
        }

        contextBuilder.Append(text[..textOffset]);

        return contextBuilder.ToString();
    }

    private static string ApplyContextOverrides(
        CompendiumRecord record,
        Prose prose,
        int? chapterIndex,
        int? sectionIndex)
    {
        var recordContext = record.Context;

        if (prose.Chapters.Count == 0 || prose.Chapters.All(c => c.Sections.Count == 0))
        {
            return recordContext;
        }

        var recordOverrides = new List<RecordOverride>();

        var endChapterIndex = chapterIndex ?? prose.Chapters.Count - 1;
        var endSectionIndex = sectionIndex ?? prose.Chapters[endChapterIndex].Sections.Count - 1;

        for (var c = 0; c <= endChapterIndex; c++)
        {
            for (var s = 0; s < prose.Chapters[c].Sections.Count; s++)
            {
                if (c == chapterIndex && s > endSectionIndex)
                {
                    break;
                }

                var overrides = prose.Chapters[c].Sections[s].RecordOverrides
                    .Where(ro => ro.CompendiumRecordId == record.Id)
                    .ToList();

                recordOverrides.AddRange(overrides);
            }
        }

        foreach (var recordOverride in recordOverrides)
        {
            if (string.IsNullOrWhiteSpace(recordOverride.Keyword))
            {
                continue;
            }

            var escapedKeyword = Regex.Escape(recordOverride.Keyword);
            var keywordRegionPattern =
                $@"(\[{escapedKeyword}\])(?:.|\n)*?(\[\/{escapedKeyword}\])";
            var regex = new Regex(keywordRegionPattern, RegexOptions.CultureInvariant);
            recordContext = regex.Replace(
                recordContext,
                match => string.Concat(
                    match.Groups[1].Value,
                    recordOverride.Description,
                    match.Groups[2].Value));
        }

        return recordContext;
    }

    private static void ProcessRecordInContext(
        CompendiumRecord record,
        IList<CompendiumRecord> records,
        HashSet<CompendiumRecord> processedRecords,
        HashSet<CompendiumRecord> recordsInContext)
    {
        if (!processedRecords.Add(record))
        {
            return;
        }

        recordsInContext.Add(record);

        foreach (var subRecord in records.Except(processedRecords).Where(r => IsRecordInContext(r, record.Context)))
        {
            ProcessRecordInContext(subRecord, records, processedRecords, recordsInContext);
        }
    }

    private static bool IsRecordInContext(CompendiumRecord record, string context)
    {
        string[] aliases =
        [
            record.Name,
            .. record.Aliases.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        ];

        foreach (var alias in aliases)
        {
            var indices = context.AllIndexesOf(alias);

            foreach (var index in indices)
            {
                if ((index == 0 || IsWordSeparator(context[index - 1]))
                    && (index + alias.Length == context.Length || IsWordSeparator(context[index + alias.Length])))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsWordSeparator(char c)
    {
        return !char.IsLetter(c)
               && !char.IsDigit(c)
               && c != '-';
    }
}
