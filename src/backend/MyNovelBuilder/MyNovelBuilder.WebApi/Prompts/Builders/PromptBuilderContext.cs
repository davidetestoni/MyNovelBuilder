using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Models.Novels;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// The context for a prompt builder.
/// </summary>
public class PromptBuilderContext<T> where T : TextGenerationContextInfoDto
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
}
