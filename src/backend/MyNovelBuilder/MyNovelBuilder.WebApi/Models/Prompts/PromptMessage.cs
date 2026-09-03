using System.ComponentModel.DataAnnotations;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Models.Prompts;

/// <summary>
/// A prompt message.
/// </summary>
public class PromptMessage
{
    /// <summary>
    /// The role of the prompt message.
    /// </summary>
    [Required]
    public required PromptMessageRole Role { get; set; }
    
    /// <summary>
    /// The message.
    /// </summary>
    [Required]
    [MaxLength(50000)]
    public required string Message { get; set; }
}
