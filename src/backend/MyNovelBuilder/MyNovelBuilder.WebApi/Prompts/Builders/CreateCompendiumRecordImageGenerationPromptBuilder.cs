using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Exceptions;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// A prompt builder for creating an image generation prompt for a compendium record.
/// </summary>
public class CreateCompendiumRecordImageGenerationPromptBuilder
    : PromptBuilder<CreateCompendiumRecordImageGenerationPromptContextInfoDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateCompendiumRecordImageGenerationPromptBuilder"/> class.
    /// </summary>
    public CreateCompendiumRecordImageGenerationPromptBuilder(string prompt) : base(prompt)
    {
    }

    /// <inheritdoc />
    public override PromptBuilder<CreateCompendiumRecordImageGenerationPromptContextInfoDto> ReplacePlaceholders(
        PromptBuilderContext<CreateCompendiumRecordImageGenerationPromptContextInfoDto> context)
    {
        base.ReplacePlaceholders(context);

        var record = context.CompendiumRecords
            .FirstOrDefault(r => r.Id == context.Client.CompendiumRecordId);

        if (record is null)
        {
            throw new ApiException(
                ErrorCodes.CompendiumRecordNotFound,
                $"Compendium record with ID {context.Client.CompendiumRecordId} not found.");
        }

        TrackIncludedRecords(context, context.CompendiumRecords);

        Builder
            .Replace("{{instructions}}", context.Client.Instructions ?? string.Empty)
            .Replace("{{record}}", CreateCompendiumRecordsString([record]))
            .Replace("{{records}}", CreateCompendiumRecordsString(context.CompendiumRecords));

        return this;
    }
}
