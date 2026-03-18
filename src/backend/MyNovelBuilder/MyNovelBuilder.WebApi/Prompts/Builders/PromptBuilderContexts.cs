using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Models.Novels;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// The context for a generic prompt builder.
/// </summary>
public class GenericPromptBuilderContext<T> where T : GenericTextGenerationContextInfoDto
{
    /// <summary>
    /// The client-provided context information.
    /// </summary>
    public required T Client { get; set; }
}

/// <summary>
/// The context for a novel-scoped prompt builder.
/// </summary>
public class NovelPromptBuilderContext<T> where T : NovelTextGenerationContextInfoDto
{
    /// <summary>
    /// The client-provided context information.
    /// </summary>
    public required T Client { get; set; }

    /// <summary>
    /// The novel.
    /// </summary>
    public required Novel Novel { get; set; }

    /// <summary>
    /// The prose of the novel.
    /// </summary>
    public required Prose Prose { get; set; }

    /// <summary>
    /// The list of compendium records available in the novel.
    /// </summary>
    public required IList<CompendiumRecord> CompendiumRecords { get; set; }

    /// <summary>
    /// The IDs of compendium records included in the final prompt.
    /// </summary>
    public required ISet<Guid> IncludedCompendiumRecordIds { get; set; }
}

/// <summary>
/// The context for a compendium-scoped prompt builder.
/// </summary>
public class CompendiumPromptBuilderContext<T> where T : CompendiumTextGenerationContextInfoDto
{
    /// <summary>
    /// The client-provided context information.
    /// </summary>
    public required T Client { get; set; }

    /// <summary>
    /// The list of compendium records available in the compendium.
    /// </summary>
    public required IList<CompendiumRecord> CompendiumRecords { get; set; }

    /// <summary>
    /// The IDs of compendium records included in the final prompt.
    /// </summary>
    public required ISet<Guid> IncludedCompendiumRecordIds { get; set; }
}
