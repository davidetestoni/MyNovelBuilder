using FluentValidation;
using MyNovelBuilder.WebApi.Models.Chats;

namespace MyNovelBuilder.WebApi.Dtos.Chat;

/// <summary>
/// Data transfer object for updating a chat.
/// </summary>
public class UpdateChatDto
{
    /// <summary>
    /// The name of the chat.
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// The index of the chapter associated with the chat context.
    /// If null, the context will include the entire novel.
    /// </summary>
    public int? ChapterIndex { get; set; }
    
    /// <summary>
    /// The ids of the compendia included in the chat context.
    /// All records from these compendia are considered when generating responses.
    /// The compendia must belong to the specified novel.
    /// </summary>
    public IEnumerable<Guid> CompendiumIds { get; set; } = [];

    /// <summary>
    /// The ids of the specific compendium records included in the chat context.
    /// The records must belong to compendia associated with the specified novel.
    /// </summary>
    public IEnumerable<Guid> CompendiumRecordIds { get; set; } = [];
    
    /// <summary>
    /// The messages in the chat conversation.
    /// </summary>
    public IEnumerable<ChatMessage> Messages { get; init; } = [];
}

internal class UpdateChatDtoValidator : AbstractValidator<UpdateChatDto>
{
    public UpdateChatDtoValidator()
    {
        RuleFor(x => x.Name).MaximumLength(200);
        RuleFor(x => x.ChapterIndex).GreaterThanOrEqualTo(0).When(x => x.ChapterIndex.HasValue);
        RuleForEach(x => x.CompendiumIds).NotEmpty();
        RuleForEach(x => x.CompendiumRecordIds).NotEmpty();
        RuleForEach(x => x.Messages).SetValidator(new ChatMessageValidator());
    }
}

internal class ChatMessageValidator : AbstractValidator<ChatMessage>
{
    public ChatMessageValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.SentAt).NotEmpty();
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.TextContent).NotEmpty().MaximumLength(50_000);
    }
}
