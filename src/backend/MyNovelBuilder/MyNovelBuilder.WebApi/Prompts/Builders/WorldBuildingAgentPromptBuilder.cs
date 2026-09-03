using System.Text;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Extensions;
using MyNovelBuilder.WebApi.Models.Novels;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// Prompt builder for the world-building agent.
/// </summary>
public class WorldBuildingAgentPromptBuilder : PromptBuilderBase
{
    /// <summary>
    /// Creates a new world-building prompt builder.
    /// </summary>
    public WorldBuildingAgentPromptBuilder(string prompt) : base(prompt)
    {
    }

    /// <summary>
    /// Replaces world-building placeholders.
    /// </summary>
    public WorldBuildingAgentPromptBuilder ReplacePlaceholders(
        WorldBuildingAgentPromptBuilderContext context)
    {
        Builder
            .Replace("{{premise}}", context.Client.FreeformPremise ?? string.Empty)
            .Replace("{{novel}}", BuildNovelContext(context.Novel))
            .Replace("{{prose}}", BuildProseContext(context.Prose, context.Client.ChapterIndex))
            .Replace("{{compendia}}", BuildCompendiaContext(context.Compendia))
            .Replace("{{records}}", BuildRecordsContext(context.CompendiumRecords))
            .Replace("{{proposalHistory}}", BuildProposalHistory(context.Client.PreviousProposals))
            .Replace("{{chatHistory}}", BuildChatHistory(context.Client.PreviousMessages))
            .Replace("{{instructions}}", context.Client.UserMessage);

        PromptBuilderUtils.TrackIncludedRecords(
            context.IncludedCompendiumRecordIds,
            context.CompendiumRecords);

        return this;
    }

    private static string BuildNovelContext(Novel? novel)
    {
        if (novel is null)
        {
            return "No novel selected.";
        }

        return $"""
                Title: {novel.Title}
                Author: {novel.Author}
                Brief: {novel.Brief}
                Language: {novel.Language}
                Tense: {novel.Tense}
                POV: {novel.Pov}
                """;
    }

    private static string BuildProseContext(Prose? prose, int? chapterIndex)
    {
        if (prose is null)
        {
            return "No prose selected.";
        }

        if (chapterIndex.HasValue)
        {
            return PromptBuilderUtils.GetWholeChapter(prose, chapterIndex.Value);
        }

        return PromptBuilderUtils.GetEntireStory(prose);
    }

    private static string BuildCompendiaContext(IEnumerable<Compendium> compendia)
    {
        var builder = new StringBuilder();

        foreach (var compendium in compendia)
        {
            builder.AppendLine($"{compendium.Name} ({compendium.Id})");
            builder.AppendLine(compendium.Description);

            var records = compendium.Records.ToList();
            if (records.Count > 0)
            {
                builder.AppendLine("Records available for update targets:");

                foreach (var record in records.OrderBy(record => record.Name))
                {
                    builder.AppendLine($"- {record.Name} ({record.Type}) - Record ID: {record.Id}");
                }
            }

            builder.AppendLine();
        }

        return builder.Length == 0 ? "No compendia selected." : builder.ToString();
    }

    private static string BuildRecordsContext(IEnumerable<CompendiumRecord> records)
    {
        var builder = new StringBuilder();

        foreach (var record in records)
        {
            builder.AppendLine($"Name: {record.Name}");
            builder.AppendLine($"Record ID: {record.Id}");
            builder.AppendLine($"Type: {record.Type}");
            builder.AppendLine($"Aliases: {record.Aliases}");
            builder.AppendLine($"Always included: {record.AlwaysIncluded}");

            if (record.Compendium is not null)
            {
                builder.AppendLine($"Compendium: {record.Compendium.Name} ({record.Compendium.Id})");
            }

            builder.AppendLine("Context:");
            builder.AppendLine(record.Context);
            builder.AppendLine();
        }

        return builder.Length == 0 ? "No records selected." : builder.ToString();
    }

    private static string BuildProposalHistory(IEnumerable<WorldBuildingProposalSummaryDto> proposals)
    {
        var builder = new StringBuilder();

        foreach (var proposal in proposals)
        {
            builder.AppendLine($"{proposal.Status}: {proposal.Kind} - {proposal.Name}");

            if (!string.IsNullOrWhiteSpace(proposal.Rationale))
            {
                builder.AppendLine($"Rationale: {proposal.Rationale}");
            }

            builder.AppendLine();
        }

        return builder.Length == 0 ? "No prior proposals." : builder.ToString();
    }

    private static string BuildChatHistory(IEnumerable<ChatMessageDto> previousMessages)
    {
        var builder = new StringBuilder();

        foreach (var message in previousMessages)
        {
            var content = string.IsNullOrWhiteSpace(message.StructuredContent)
                ? message.TextContent.StripHtml()
                : message.StructuredContent;
            builder.AppendLine($"{message.Role}: {content}");
            builder.AppendLine();
        }

        return builder.ToString();
    }
}

/// <summary>
/// Context for the world-building prompt builder.
/// </summary>
public class WorldBuildingAgentPromptBuilderContext
{
    /// <summary>
    /// Client-provided context.
    /// </summary>
    public required WorldBuildingAgentContextInfoDto Client { get; set; }

    /// <summary>
    /// Optional novel.
    /// </summary>
    public Novel? Novel { get; set; }

    /// <summary>
    /// Optional prose.
    /// </summary>
    public Prose? Prose { get; set; }

    /// <summary>
    /// Selected compendia.
    /// </summary>
    public required IList<Compendium> Compendia { get; set; }

    /// <summary>
    /// Selected records.
    /// </summary>
    public required IList<CompendiumRecord> CompendiumRecords { get; set; }

    /// <summary>
    /// Included record IDs.
    /// </summary>
    public required ISet<Guid> IncludedCompendiumRecordIds { get; set; }
}
