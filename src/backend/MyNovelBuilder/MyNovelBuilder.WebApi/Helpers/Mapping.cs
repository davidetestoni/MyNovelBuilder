using Mapster;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.CompendiumRecord;
using MyNovelBuilder.WebApi.Dtos.Novel;

namespace MyNovelBuilder.WebApi.Helpers;

/// <summary>
/// Helper class for mapping.
/// </summary>
public static class Mapping
{
    /// <summary>
    /// Configure Mapster.
    /// </summary>
    public static void ConfigureMapster(TypeAdapterConfig config)
    {
        config.NewConfig<Novel, NovelDto>()
            .Map(dest => dest.CompendiumIds, src => src.Compendia.Select(c => c.Id).ToList());
        
        config.NewConfig<CompendiumRecord, CompendiumRecordDto>()
            .Map(dest => dest.CompendiumId, src => src.Compendium.Id);
    }
}
