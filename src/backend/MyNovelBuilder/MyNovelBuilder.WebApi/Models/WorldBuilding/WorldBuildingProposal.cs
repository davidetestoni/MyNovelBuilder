namespace MyNovelBuilder.WebApi.Models.WorldBuilding;

/// <summary>
/// A pending, accepted, or rejected world-building operation.
/// </summary>
public class WorldBuildingProposal
{
    /// <summary>
    /// The proposal ID.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The assistant message that introduced this proposal.
    /// </summary>
    public Guid? MessageId { get; set; }

    /// <summary>
    /// Current proposal status.
    /// </summary>
    public required WorldBuildingProposalStatus Status { get; set; }

    /// <summary>
    /// Operation to apply if accepted.
    /// </summary>
    public required WorldBuildingOperation Operation { get; set; }

    /// <summary>
    /// Why the agent proposed this operation.
    /// </summary>
    public string? Rationale { get; set; }

    /// <summary>
    /// Entity created or updated when the proposal was accepted.
    /// </summary>
    public Guid? AppliedEntityId { get; set; }

    /// <summary>
    /// The time the proposal was accepted and applied.
    /// </summary>
    public DateTime? AppliedAt { get; set; }
}
