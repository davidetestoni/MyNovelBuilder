using System.Text;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Extensions;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// Prompt builder for preparing immersive multi-speaker TTS chunks.
/// </summary>
public class PrepareImmersiveTtsPromptBuilder : NovelPromptBuilder<PrepareImmersiveTtsContextInfoDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PrepareImmersiveTtsPromptBuilder"/> class.
    /// </summary>
    public PrepareImmersiveTtsPromptBuilder(string prompt) : base(prompt)
    {
    }

    /// <inheritdoc />
    public override NovelPromptBuilder<PrepareImmersiveTtsContextInfoDto> ReplacePlaceholders(
        NovelPromptBuilderContext<PrepareImmersiveTtsContextInfoDto> context)
    {
        base.ReplacePlaceholders(context);

        var chapter = PromptBuilderUtils.GetChapter(context.Prose, context.Client.ChapterIndex);
        var section = PromptBuilderUtils.GetSection(chapter, context.Client.SectionIndex);
        var sectionText = section?.Text.StripHtml() ?? string.Empty;
        var chapterContext = PromptBuilderUtils.GetWholeChapter(context.Prose, context.Client.ChapterIndex);
        var storySoFar = PromptBuilderUtils.GetStorySoFar(
            context.Prose,
            context.Client.ChapterIndex,
            context.Client.SectionIndex,
            sectionText.Length);

        var recordsInContext = PromptBuilderUtils.FilterRecordsInContext(
            context.CompendiumRecords,
            string.Join(
                "\n\n",
                [
                    chapter.Title,
                    section?.Summary ?? string.Empty,
                    sectionText,
                    chapterContext
                ]));

        var recordsInContextList = recordsInContext.ToList();
        PromptBuilderUtils.TrackIncludedRecords(
            context.IncludedCompendiumRecordIds,
            recordsInContextList);

        Builder
            .Replace("{{chapterTitle}}", chapter.Title.StripHtml())
            .Replace("{{sectionSummary}}", section?.Summary ?? string.Empty)
            .Replace("{{sectionText}}", sectionText)
            .Replace("{{storySoFar}}", storySoFar)
            .Replace("{{wholeChapter}}", chapterContext)
            .Replace("{{records}}", PromptBuilderUtils.CreateCompendiumRecordsString(
                recordsInContextList,
                (
                    context.Prose,
                    context.Client.ChapterIndex,
                    context.Client.SectionIndex
                )))
            .Replace("{{speakerOptions}}", CreateSpeakerOptionsString(
                recordsInContextList,
                context.Client.Provider,
                context.Client.TtsModelId));

        return this;
    }

    private static string CreateSpeakerOptionsString(
        IEnumerable<CompendiumRecord> records,
        TtsProvider provider,
        string modelId)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Narrator");
        builder.AppendLine("- speakerKind: narrator");
        builder.AppendLine("- speakerName: Narrator");
        builder.AppendLine("- characterRecordId: null");
        builder.AppendLine();

        foreach (var record in records
                     .Where(r => r.Type == CompendiumRecordType.Character)
                     .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
        {
            var assignment = record.CharacterVoiceAssignments.FirstOrDefault(a =>
                a.Provider == provider
                && string.Equals(a.ModelId, modelId, StringComparison.Ordinal));

            if (assignment is null)
            {
                continue;
            }

            builder.AppendLine(record.Name);
            builder.AppendLine("- speakerKind: character");
            builder.AppendLine($"- speakerName: {record.Name}");
            builder.AppendLine($"- characterRecordId: {record.Id}");

            if (!string.IsNullOrWhiteSpace(record.Aliases))
            {
                builder.AppendLine($"- aliases: {record.Aliases}");
            }

            builder.AppendLine($"- provider: {provider}");
            builder.AppendLine($"- modelId: {modelId}");
            builder.AppendLine($"- voiceId: {assignment.VoiceId}");

            if (!string.IsNullOrWhiteSpace(assignment.VoiceName))
            {
                builder.AppendLine($"- voiceName: {assignment.VoiceName}");
            }

            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }
}
