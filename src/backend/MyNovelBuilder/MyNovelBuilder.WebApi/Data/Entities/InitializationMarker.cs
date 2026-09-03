using System.ComponentModel.DataAnnotations;

namespace MyNovelBuilder.WebApi.Data.Entities;

/// <summary>
/// Records that a one-time database initialization step has completed.
/// </summary>
public sealed class InitializationMarker
{
    /// <summary>
    /// The unique initialization step name.
    /// </summary>
    [Key]
    [MaxLength(100)]
    public required string Key { get; init; }

    /// <summary>
    /// The time at which the initialization step completed.
    /// </summary>
    public DateTime CompletedAtUtc { get; init; }
}
