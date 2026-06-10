using System.Text.Json;
using System.Text.Json.Serialization;
using Mapster;
using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Compendium;
using MyNovelBuilder.WebApi.Dtos.CompendiumRecord;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Dtos.WorldBuilding;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.Chats;
using MyNovelBuilder.WebApi.Models.WorldBuilding;
using MyNovelBuilder.WebApi.Options;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// File-backed service for world-building sessions.
/// </summary>
public class WorldBuildingSessionService : IWorldBuildingSessionService
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly string _dataFolder;
    private readonly IPromptService _promptService;
    private readonly IWorldBuildingPromptCreatorService _promptCreatorService;
    private readonly ITextGenerationServiceResolver _textGenerationServiceResolver;
    private readonly ICompendiumService _compendiumService;
    private readonly ICompendiumRecordService _compendiumRecordService;
    private readonly ITokenizerService _tokenizerService;

    /// <summary></summary>
    public WorldBuildingSessionService(
        IOptions<AppStorageOptions> storageOptions,
        IPromptService promptService,
        IWorldBuildingPromptCreatorService promptCreatorService,
        ITextGenerationServiceResolver textGenerationServiceResolver,
        ICompendiumService compendiumService,
        ICompendiumRecordService compendiumRecordService,
        ITokenizerService tokenizerService)
    {
        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        _jsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        _dataFolder = storageOptions.Value.DataFolder;
        _promptService = promptService;
        _promptCreatorService = promptCreatorService;
        _textGenerationServiceResolver = textGenerationServiceResolver;
        _compendiumService = compendiumService;
        _compendiumRecordService = compendiumRecordService;
        _tokenizerService = tokenizerService;
    }

    private string GetSessionsDirectoryPath() =>
        Path.Combine(_dataFolder, "world-building-sessions");

    private string GetSessionFilePath(Guid id) =>
        Path.Combine(GetSessionsDirectoryPath(), $"{id}.json");

    /// <inheritdoc />
    public async Task<WorldBuildingSession> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var path = GetSessionFilePath(id);

        if (!File.Exists(path))
        {
            throw new ApiException(
                ErrorCodes.WorldBuildingSessionNotFound,
                $"World-building session with ID {id} was not found.");
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<WorldBuildingSession>(json, _jsonSerializerOptions)!;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<WorldBuildingSessionMetadata>> GetAllMetadataAsync(
        CancellationToken cancellationToken = default)
    {
        var path = GetSessionsDirectoryPath();
        Directory.CreateDirectory(path);

        var metadataList = new List<WorldBuildingSessionMetadata>();

        foreach (var file in Directory.GetFiles(path, "*.json"))
        {
            var json = await File.ReadAllTextAsync(file, cancellationToken);
            var session = JsonSerializer.Deserialize<WorldBuildingSession>(json, _jsonSerializerOptions)!;
            metadataList.Add(new WorldBuildingSessionMetadata
            {
                Id = Path.GetFileNameWithoutExtension(file) is { } fileName
                    ? Guid.Parse(fileName)
                    : Guid.Empty,
                NovelId = session.Context.NovelId,
                CreatedAt = session.CreatedAt,
                UpdatedAt = session.UpdatedAt,
                Name = session.Name
            });
        }

        return metadataList
            .OrderByDescending(metadata => metadata.UpdatedAt)
            .ToList();
    }

    /// <inheritdoc />
    public async Task CreateAsync(
        WorldBuildingSession session,
        CancellationToken cancellationToken = default)
    {
        var path = GetSessionFilePath(session.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var json = JsonSerializer.Serialize(session, _jsonSerializerOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(
        Guid id,
        WorldBuildingSession session,
        CancellationToken cancellationToken = default)
    {
        var path = GetSessionFilePath(id);

        if (!File.Exists(path))
        {
            throw new ApiException(
                ErrorCodes.WorldBuildingSessionNotFound,
                $"World-building session with ID {id} was not found.");
        }

        var json = JsonSerializer.Serialize(session, _jsonSerializerOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var path = GetSessionFilePath(id);

        if (!File.Exists(path))
        {
            throw new ApiException(
                ErrorCodes.WorldBuildingSessionNotFound,
                $"World-building session with ID {id} was not found.");
        }

        File.Delete(path);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<WorldBuildingSession> SendMessageAsync(
        Guid id,
        SendWorldBuildingMessageDto dto,
        CancellationToken cancellationToken = default)
    {
        var session = await GetByIdAsync(id, cancellationToken);
        var prompt = await _promptService.GetByIdAsync(dto.PromptId, cancellationToken);
        var contextInfo = CreateContextInfo(session, dto.Message);
        var processedPrompt = await _promptCreatorService.CreatePromptAsync(
            prompt,
            contextInfo,
            cancellationToken);
        var generationService = await _textGenerationServiceResolver.GetConfiguredServiceAsync(
            cancellationToken);
        var responseJson = await generationService.GenerateAsync(
            dto.Model,
            processedPrompt.Messages,
            contextInfo.GetStructuredOutputOptions(),
            cancellationToken);
        var agentResponse = JsonSerializer.Deserialize<WorldBuildingAgentResponseDto>(
            responseJson,
            _jsonSerializerOptions);

        if (agentResponse is null)
        {
            throw new ApiException(
                ErrorCodes.ExternalServiceError,
                "The world-building agent returned an invalid response.");
        }

        var userMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            SentAt = DateTime.UtcNow,
            Role = ChatMessageRole.User,
            TextContent = dto.Message
        };
        var assistantMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            SentAt = DateTime.UtcNow,
            Role = ChatMessageRole.Assistant,
            TextContent = agentResponse.AssistantMessage
        };

        session.Messages.Add(userMessage);
        session.Messages.Add(assistantMessage);
        session.Name ??= CreateDefaultName(dto.Message);

        foreach (var proposalDto in agentResponse.Proposals)
        {
            var operation = proposalDto.ToOperation();
            NormalizeOperation(operation, session.Context);
            session.Proposals.Add(new WorldBuildingProposal
            {
                Id = Guid.NewGuid(),
                MessageId = assistantMessage.Id,
                Status = WorldBuildingProposalStatus.Pending,
                Operation = operation,
                Rationale = proposalDto.Rationale
            });
        }

        session.UpdatedAt = DateTime.UtcNow;
        await UpdateAsync(id, session, cancellationToken);
        return session;
    }

    /// <inheritdoc />
    public async Task<WorldBuildingSession> DeleteMessageAsync(
        Guid id,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetByIdAsync(id, cancellationToken);

        if (!session.Messages.Any(message => message.Id == messageId))
        {
            throw new ApiException(
                ErrorCodes.BadRequest,
                $"Message with ID {messageId} was not found.");
        }

        session.Messages = session.Messages
            .Where(message => message.Id != messageId)
            .ToList();
        session.Proposals = session.Proposals
            .Where(proposal => proposal.MessageId != messageId)
            .ToList();
        session.UpdatedAt = DateTime.UtcNow;

        await UpdateAsync(id, session, cancellationToken);
        return session;
    }

    /// <inheritdoc />
    public async Task<WorldBuildingSession> UpdateProposalAsync(
        Guid id,
        Guid proposalId,
        UpdateWorldBuildingProposalDto dto,
        CancellationToken cancellationToken = default)
    {
        var session = await GetByIdAsync(id, cancellationToken);
        var proposal = GetProposal(session, proposalId);
        EnsurePending(proposal);

        NormalizeOperation(dto.Operation, session.Context);
        proposal.Operation = dto.Operation;
        proposal.Rationale = dto.Rationale;
        session.UpdatedAt = DateTime.UtcNow;

        await UpdateAsync(id, session, cancellationToken);
        return session;
    }

    /// <inheritdoc />
    public async Task<WorldBuildingSession> AcceptProposalAsync(
        Guid id,
        Guid proposalId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetByIdAsync(id, cancellationToken);
        var proposal = GetProposal(session, proposalId);
        EnsurePending(proposal);

        proposal.AppliedEntityId = await ApplyProposalAsync(proposal, cancellationToken);
        proposal.AppliedAt = DateTime.UtcNow;
        proposal.Status = WorldBuildingProposalStatus.Accepted;
        session.UpdatedAt = DateTime.UtcNow;

        await UpdateAsync(id, session, cancellationToken);
        return session;
    }

    /// <inheritdoc />
    public async Task<WorldBuildingSession> RejectProposalAsync(
        Guid id,
        Guid proposalId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetByIdAsync(id, cancellationToken);
        var proposal = GetProposal(session, proposalId);
        EnsurePending(proposal);

        proposal.Status = WorldBuildingProposalStatus.Rejected;
        session.UpdatedAt = DateTime.UtcNow;

        await UpdateAsync(id, session, cancellationToken);
        return session;
    }

    private WorldBuildingAgentContextInfoDto CreateContextInfo(
        WorldBuildingSession session,
        string userMessage)
    {
        return new WorldBuildingAgentContextInfoDto
        {
            NovelId = session.Context.NovelId,
            ChapterIndex = session.Context.ChapterIndex,
            CompendiumIds = session.Context.CompendiumIds,
            CompendiumRecordIds = session.Context.CompendiumRecordIds,
            FreeformPremise = session.Context.FreeformPremise,
            UserMessage = userMessage,
            PreviousMessages = session.Messages.Select(message => new ChatMessageDto
            {
                Role = message.Role,
                TextContent = message.TextContent
            }),
            PreviousProposals = session.Proposals.Select(proposal => new WorldBuildingProposalSummaryDto
            {
                Status = proposal.Status.ToString(),
                Kind = proposal.Operation.Kind.ToString(),
                Name = proposal.Operation.Name,
                Rationale = proposal.Rationale
            })
        };
    }

    private async Task<Guid> ApplyProposalAsync(
        WorldBuildingProposal proposal,
        CancellationToken cancellationToken)
    {
        var operation = proposal.Operation;

        switch (operation.Kind)
        {
            case WorldBuildingOperationKind.CreateCompendium:
            {
                var compendium = new CreateCompendiumDto
                {
                    Name = operation.Name,
                    Description = operation.Description
                }.Adapt<Compendium>();

                await _compendiumService.CreateAsync(compendium, cancellationToken);
                return compendium.Id;
            }
            case WorldBuildingOperationKind.UpdateCompendium:
            {
                if (!operation.TargetCompendiumId.HasValue)
                {
                    throw new ApiException(ErrorCodes.BadRequest, "Target compendium ID is required.");
                }

                var compendium = await _compendiumService.GetByIdAsync(
                    operation.TargetCompendiumId.Value,
                    cancellationToken);
                new UpdateCompendiumDto
                {
                    Id = operation.TargetCompendiumId.Value,
                    Name = operation.Name,
                    Description = operation.Description
                }.Adapt(compendium);

                await _compendiumService.UpdateAsync(compendium, cancellationToken);
                return compendium.Id;
            }
            case WorldBuildingOperationKind.CreateCompendiumRecord:
            {
                if (!operation.TargetCompendiumId.HasValue)
                {
                    throw new ApiException(ErrorCodes.BadRequest, "Target compendium ID is required.");
                }

                var compendium = await _compendiumService.GetByIdAsync(
                    operation.TargetCompendiumId.Value,
                    cancellationToken);
                var record = new CreateCompendiumRecordDto
                {
                    Name = operation.Name,
                    Aliases = operation.Aliases,
                    Type = operation.Type,
                    Context = operation.Context,
                    CompendiumId = operation.TargetCompendiumId.Value,
                    AlwaysIncluded = operation.AlwaysIncluded
                }.Adapt<CompendiumRecord>();
                record.Compendium = compendium;
                record.ContextTokenCount = _tokenizerService.CountTokens(record.Context);

                await _compendiumRecordService.CreateAsync(record, cancellationToken);
                return record.Id;
            }
            case WorldBuildingOperationKind.UpdateCompendiumRecord:
            {
                if (!operation.TargetRecordId.HasValue)
                {
                    throw new ApiException(ErrorCodes.BadRequest, "Target record ID is required.");
                }

                var record = await _compendiumRecordService.GetByIdAsync(
                    operation.TargetRecordId.Value,
                    cancellationToken);
                new UpdateCompendiumRecordDto
                {
                    Id = operation.TargetRecordId.Value,
                    Name = operation.Name,
                    Aliases = operation.Aliases,
                    Type = operation.Type,
                    Context = operation.Context,
                    AlwaysIncluded = operation.AlwaysIncluded
                }.Adapt(record);
                record.ContextTokenCount = _tokenizerService.CountTokens(record.Context);

                await _compendiumRecordService.UpdateAsync(record, cancellationToken);
                return record.Id;
            }
            default:
                throw new ApiException(ErrorCodes.BadRequest, "Unsupported world-building proposal operation.");
        }
    }

    private static WorldBuildingProposal GetProposal(
        WorldBuildingSession session,
        Guid proposalId)
    {
        return session.Proposals.FirstOrDefault(proposal => proposal.Id == proposalId)
            ?? throw new ApiException(
                ErrorCodes.WorldBuildingProposalNotFound,
                $"World-building proposal with ID {proposalId} was not found.");
    }

    private static void EnsurePending(WorldBuildingProposal proposal)
    {
        if (proposal.Status != WorldBuildingProposalStatus.Pending)
        {
            throw new ApiException(ErrorCodes.BadRequest, "Only pending proposals can be changed.");
        }
    }

    private static void NormalizeOperation(
        WorldBuildingOperation operation,
        WorldBuildingContext context)
    {
        if (operation.Kind == WorldBuildingOperationKind.CreateCompendiumRecord
            && !operation.TargetCompendiumId.HasValue
            && context.CompendiumIds.Count == 1)
        {
            operation.TargetCompendiumId = context.CompendiumIds[0];
        }

        if (operation.Kind == WorldBuildingOperationKind.UpdateCompendiumRecord
            && !operation.TargetRecordId.HasValue
            && context.CompendiumRecordIds.Count == 1)
        {
            operation.TargetRecordId = context.CompendiumRecordIds[0];
        }

        operation.Name = Truncate(operation.Name.Trim(), 100);
        operation.Description = Truncate(operation.Description.Trim(), 500);
        operation.Aliases = Truncate(operation.Aliases.Trim(), 500);
        operation.Context = Truncate(operation.Context.Trim(), 10_000);
    }

    private static string CreateDefaultName(string message)
    {
        return Truncate(message.Trim().ReplaceLineEndings(" "), 60);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
