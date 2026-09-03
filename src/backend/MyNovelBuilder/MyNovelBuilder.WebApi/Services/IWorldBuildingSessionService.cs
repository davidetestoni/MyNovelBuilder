using MyNovelBuilder.WebApi.Dtos.WorldBuilding;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Models.WorldBuilding;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for world-building agent sessions.
/// </summary>
public interface IWorldBuildingSessionService
{
    /// <summary>
    /// Get a session by ID.
    /// </summary>
    Task<WorldBuildingSession> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all session metadata.
    /// </summary>
    Task<IEnumerable<WorldBuildingSessionMetadata>> GetAllMetadataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a session.
    /// </summary>
    Task CreateAsync(WorldBuildingSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update a session.
    /// </summary>
    Task UpdateAsync(Guid id, WorldBuildingSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a session.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a message to the world-building agent.
    /// </summary>
    Task<WorldBuildingSession> SendMessageAsync(
        Guid id,
        SendWorldBuildingMessageDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Preview the exact prompt that would be used for a world-building message.
    /// </summary>
    Task<TextGenerationPreviewDto> GetMessagePreviewAsync(
        Guid id,
        SendWorldBuildingMessageDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a message and any proposals attached to it.
    /// </summary>
    Task<WorldBuildingSession> DeleteMessageAsync(
        Guid id,
        Guid messageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update a pending proposal.
    /// </summary>
    Task<WorldBuildingSession> UpdateProposalAsync(
        Guid id,
        Guid proposalId,
        UpdateWorldBuildingProposalDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Accept and apply a proposal.
    /// </summary>
    Task<WorldBuildingSession> AcceptProposalAsync(
        Guid id,
        Guid proposalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reject a proposal.
    /// </summary>
    Task<WorldBuildingSession> RejectProposalAsync(
        Guid id,
        Guid proposalId,
        CancellationToken cancellationToken = default);
}
