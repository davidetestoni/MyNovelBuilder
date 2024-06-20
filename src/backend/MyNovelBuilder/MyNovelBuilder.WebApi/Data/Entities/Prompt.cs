using System.ComponentModel.DataAnnotations;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.Prompts;

namespace MyNovelBuilder.WebApi.Data.Entities;

/// <summary>
/// A prompt.
/// </summary>
public class Prompt : TimestampedEntity
{
    /// <summary>
    /// The prompt's name.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string Name { get; init; }
    
    /// <summary>
    /// The prompt type.
    /// </summary>
    [Required]
    public PromptType Type { get; init; }
    
    /// <summary>
    /// The prompt's messages.
    /// </summary>
    [Required]
    public IEnumerable<PromptMessage> Messages { get; set; } = new List<PromptMessage>();
}
