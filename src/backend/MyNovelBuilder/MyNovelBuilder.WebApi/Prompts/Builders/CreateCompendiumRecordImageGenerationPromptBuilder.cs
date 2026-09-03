using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Exceptions;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// A prompt builder for creating an image generation prompt for a compendium record.
/// </summary>
public class CreateCompendiumRecordImageGenerationPromptBuilder
    : CompendiumPromptBuilder<CreateCompendiumRecordImageGenerationPromptContextInfoDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateCompendiumRecordImageGenerationPromptBuilder"/> class.
    /// </summary>
    public CreateCompendiumRecordImageGenerationPromptBuilder(string prompt) : base(prompt)
    {
    }

    /// <inheritdoc />
    public override CompendiumPromptBuilder<CreateCompendiumRecordImageGenerationPromptContextInfoDto> ReplacePlaceholders(
        CompendiumPromptBuilderContext<CreateCompendiumRecordImageGenerationPromptContextInfoDto> context)
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

        PromptBuilderUtils.TrackIncludedRecords(
            context.IncludedCompendiumRecordIds,
            context.CompendiumRecords);

        Builder
            .Replace("{{instructions}}", context.Client.Instructions ?? string.Empty)
            .Replace("{{record}}", PromptBuilderUtils.CreateCompendiumRecordsString([record]))
            .Replace("{{records}}", PromptBuilderUtils.CreateCompendiumRecordsString(
                context.CompendiumRecords));

        return this;
    }
}
