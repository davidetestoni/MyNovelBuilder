using FluentValidation;

namespace MyNovelBuilder.WebApi.Dtos.WorldBuilding;

/// <summary>
/// DTO for sending a message to the world-building agent.
/// </summary>
public class SendWorldBuildingMessageDto
{
    /// <summary>
    /// The text-generation model to use.
    /// </summary>
    public required string Model { get; set; }

    /// <summary>
    /// The world-building agent prompt ID.
    /// </summary>
    public required Guid PromptId { get; set; }

    /// <summary>
    /// The user's message.
    /// </summary>
    public required string Message { get; set; }
}

internal class SendWorldBuildingMessageDtoValidator : AbstractValidator<SendWorldBuildingMessageDto>
{
    public SendWorldBuildingMessageDtoValidator()
    {
        RuleFor(x => x.Model).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PromptId).NotEmpty();
        RuleFor(x => x.Message).NotEmpty().MaximumLength(50_000);
    }
}
