using Mapster;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.CompendiumRecord;
using MyNovelBuilder.WebApi.Dtos.Novel;

namespace MyNovelBuilder.WebApi.Helpers;

/// <summary>
/// Helper class for mapping.
/// </summary>
public class MappingConfig : IRegister
{
    /// <inheritdoc />
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Novel, NovelDto>()
            .Map(dest => dest.MainCharacterId, src => src.MainCharacter != null ? src.MainCharacter.Id : (Guid?)null)
            .Map(dest => dest.CompendiumIds, src => src.Compendia.Select(c => c.Id).ToList());
        
        config.NewConfig<CompendiumRecord, CompendiumRecordDto>()
            .Map(dest => dest.CompendiumId, src => src.Compendium.Id);
    }
}
