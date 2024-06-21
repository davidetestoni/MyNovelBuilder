using System.ComponentModel.DataAnnotations;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Data.Entities;

/// <summary>
/// A novel.
/// </summary>
public class Novel : TimestampedEntity
{
    /// <summary>
    /// The novel's title.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string Title { get; init; }
    
    /// <summary>
    /// The novel's author.
    /// </summary>
    [MaxLength(100)]
    public string Author { get; init; } = string.Empty;
    
    /// <summary>
    /// A brief description of the novel.
    /// </summary>
    [MaxLength(500)]
    public string Brief { get; init; } = string.Empty;
    
    /// <summary>
    /// The novel's writing tense.
    /// </summary>
    public WritingTense Tense { get; init; } = WritingTense.Present;
    
    /// <summary>
    /// The novel's writing point of view.
    /// </summary>
    public WritingPov Pov { get; init; } = WritingPov.FirstPerson;
    
    /// <summary>
    /// The novel's writing language.
    /// </summary>
    public WritingLanguage Language { get; init; } = WritingLanguage.English;
    
    /// <summary>
    /// The main character of the novel, if any.
    /// It must be of type <see cref="CompendiumRecordType.Character"/>.
    /// </summary>
    public CompendiumRecord? MainCharacter { get; set; }
    
    /// <summary>
    /// The compendia used in the novel.
    /// </summary>
    public IEnumerable<Compendium> Compendia { get; set; } = new List<Compendium>();
}
