namespace MyNovelBuilder.WebApi.Models.WorldBuilding;

/// <summary>
/// The lifecycle state of a world-building proposal.
/// </summary>
public enum WorldBuildingProposalStatus
{
    /// <summary>
    /// The proposal is waiting for user review.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The proposal was accepted and applied.
    /// </summary>
    Accepted = 1,

    /// <summary>
    /// The proposal was rejected.
    /// </summary>
    Rejected = 2
}
