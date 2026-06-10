using Microsoft.AspNetCore.Mvc;
using MyNovelBuilder.WebApi.Dtos.WorldBuilding;
using MyNovelBuilder.WebApi.Models.WorldBuilding;
using MyNovelBuilder.WebApi.Services;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>
/// Controller for world-building agent sessions.
/// </summary>
[Route("api/world-building-agent/session")]
[ApiController]
public class WorldBuildingAgentController : ControllerBase
{
    private readonly IWorldBuildingSessionService _worldBuildingSessionService;

    /// <summary></summary>
    public WorldBuildingAgentController(
        IWorldBuildingSessionService worldBuildingSessionService)
    {
        _worldBuildingSessionService = worldBuildingSessionService;
    }

    /// <summary>
    /// Get metadata for all world-building sessions.
    /// </summary>
    [HttpGet("/api/world-building-agent/sessions")]
    public async Task<IEnumerable<WorldBuildingSessionMetadata>> GetAllSessionMetadata(
        CancellationToken cancellationToken = default)
    {
        return await _worldBuildingSessionService.GetAllMetadataAsync(cancellationToken);
    }

    /// <summary>
    /// Get a world-building session by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<WorldBuildingSession> GetSessionById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _worldBuildingSessionService.GetByIdAsync(id, cancellationToken);
    }

    /// <summary>
    /// Create a world-building session.
    /// </summary>
    [HttpPost]
    public async Task<WorldBuildingSession> CreateSession(
        CreateWorldBuildingSessionDto dto,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var session = new WorldBuildingSession
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            UpdatedAt = now,
            Name = dto.Name,
            Context = new WorldBuildingContext
            {
                NovelId = dto.NovelId,
                ChapterIndex = dto.ChapterIndex,
                CompendiumIds = dto.CompendiumIds.ToList(),
                CompendiumRecordIds = dto.CompendiumRecordIds.ToList(),
                FreeformPremise = dto.FreeformPremise
            }
        };

        await _worldBuildingSessionService.CreateAsync(session, cancellationToken);
        return session;
    }

    /// <summary>
    /// Update a world-building session.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task UpdateSession(
        Guid id,
        UpdateWorldBuildingSessionDto dto,
        CancellationToken cancellationToken = default)
    {
        var session = await _worldBuildingSessionService.GetByIdAsync(id, cancellationToken);
        session.UpdatedAt = DateTime.UtcNow;
        session.Name = dto.Name;
        session.Context.NovelId = dto.NovelId;
        session.Context.ChapterIndex = dto.ChapterIndex;
        session.Context.CompendiumIds = dto.CompendiumIds.ToList();
        session.Context.CompendiumRecordIds = dto.CompendiumRecordIds.ToList();
        session.Context.FreeformPremise = dto.FreeformPremise;
        session.Messages = dto.Messages.ToList();

        await _worldBuildingSessionService.UpdateAsync(id, session, cancellationToken);
    }

    /// <summary>
    /// Delete a world-building session.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task DeleteSession(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await _worldBuildingSessionService.DeleteAsync(id, cancellationToken);
    }

    /// <summary>
    /// Delete a message and any proposals attached to it.
    /// </summary>
    [HttpDelete("{id:guid}/message/{messageId:guid}")]
    public async Task<WorldBuildingSession> DeleteMessage(
        Guid id,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        return await _worldBuildingSessionService.DeleteMessageAsync(
            id,
            messageId,
            cancellationToken);
    }

    /// <summary>
    /// Send a message to the world-building agent.
    /// </summary>
    [HttpPost("{id:guid}/message")]
    public async Task<WorldBuildingSession> SendMessage(
        Guid id,
        SendWorldBuildingMessageDto dto,
        CancellationToken cancellationToken = default)
    {
        return await _worldBuildingSessionService.SendMessageAsync(id, dto, cancellationToken);
    }

    /// <summary>
    /// Update a pending proposal.
    /// </summary>
    [HttpPut("{id:guid}/proposal/{proposalId:guid}")]
    public async Task<WorldBuildingSession> UpdateProposal(
        Guid id,
        Guid proposalId,
        UpdateWorldBuildingProposalDto dto,
        CancellationToken cancellationToken = default)
    {
        return await _worldBuildingSessionService.UpdateProposalAsync(
            id,
            proposalId,
            dto,
            cancellationToken);
    }

    /// <summary>
    /// Accept and apply a pending proposal.
    /// </summary>
    [HttpPost("{id:guid}/proposal/{proposalId:guid}/accept")]
    public async Task<WorldBuildingSession> AcceptProposal(
        Guid id,
        Guid proposalId,
        CancellationToken cancellationToken = default)
    {
        return await _worldBuildingSessionService.AcceptProposalAsync(
            id,
            proposalId,
            cancellationToken);
    }

    /// <summary>
    /// Reject a pending proposal.
    /// </summary>
    [HttpPost("{id:guid}/proposal/{proposalId:guid}/reject")]
    public async Task<WorldBuildingSession> RejectProposal(
        Guid id,
        Guid proposalId,
        CancellationToken cancellationToken = default)
    {
        return await _worldBuildingSessionService.RejectProposalAsync(
            id,
            proposalId,
            cancellationToken);
    }
}
