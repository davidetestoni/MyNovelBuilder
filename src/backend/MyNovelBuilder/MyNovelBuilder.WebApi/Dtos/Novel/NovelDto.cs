using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Dtos.Novel;

/// <summary>
/// Data transfer object for a novel.
/// </summary>
public class NovelDto
{
    /// <summary>
    /// The novel's ID.
    /// </summary>
    public required Guid Id { get; set; }
    
    /// <summary>
    /// The date and time the novel was created.
    /// </summary>
    public required DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// The date and time the novel was last updated.
    /// </summary>
    public required DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// The novel's title.
    /// </summary>
    public required string Title { get; set; }
    
    /// <summary>
    /// The novel's author.
    /// </summary>
    public required string Author { get; set; }

    /// <summary>
    /// A brief description of the novel.
    /// </summary>
    public required string Brief { get; set; }
    
    /// <summary>
    /// The novel's cover image URL.
    /// </summary>
    public required string? CoverImageUrl { get; set; }
    
    /// <summary>
    /// The novel's writing tense.
    /// </summary>
    public required WritingTense Tense { get; set; }
    
    /// <summary>
    /// The novel's writing point of view.
    /// </summary>
    public required WritingPov Pov { get; set; }
    
    /// <summary>
    /// The novel's writing language.
    /// </summary>
    public required WritingLanguage Language { get; set; }
    
    /// <summary>
    /// The id of the main character of the novel, if any.
    /// If present, it must be the id of a <see cref="CompendiumRecord"/>
    /// of type <see cref="CompendiumRecordType.Character"/>.
    /// </summary>
    public Guid? MainCharacterId { get; set; }
    
    /// <summary>
    /// The ids of the compendia used in the novel.
    /// </summary>
    public required IEnumerable<Guid> CompendiumIds { get; set; } = Array.Empty<Guid>();
}
