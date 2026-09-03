using Mapster;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.CompendiumRecord;
using MyNovelBuilder.WebApi.Dtos.Novel;
using MyNovelBuilder.WebApi.Helpers;

namespace MyNovelBuilder.WebApi.Tests.Unit.Helpers;

public class MappingConfigTests
{
    private readonly TypeAdapterConfig _config;

    public MappingConfigTests()
    {
        _config = new TypeAdapterConfig();
        new MappingConfig().Register(_config);
    }

    [Fact]
    public void Novel_To_NovelDto_MapsCorrectly()
    {
        // Arrange
        var mainCharacter = new CompendiumRecord 
        { 
            Id = Guid.NewGuid(), 
            Name = "John Doe" 
        };
        
        var compendia = new List<Compendium>
        {
            new() { Id = Guid.NewGuid(), Name = "Comp 1" },
            new() { Id = Guid.NewGuid(), Name = "Comp 2" }
        };

        var novel = new Novel
        {
            Id = Guid.NewGuid(),
            Title = "My Novel",
            MainCharacter = mainCharacter,
            Compendia = compendia
        };

        // Act
        var dto = novel.Adapt<NovelDto>(_config);

        // Assert
        Assert.Equal(novel.Id, dto.Id);
        Assert.Equal(novel.Title, dto.Title);
        Assert.Equal(mainCharacter.Id, dto.MainCharacterId);
        Assert.Equal(2, dto.CompendiumIds.Count());
        Assert.Contains(compendia[0].Id, dto.CompendiumIds);
        Assert.Contains(compendia[1].Id, dto.CompendiumIds);
    }

    [Fact]
    public void CompendiumRecord_To_CompendiumRecordDto_MapsCorrectly()
    {
        // Arrange
        var compendium = new Compendium { Id = Guid.NewGuid(), Name = "My Comp" };
        var record = new CompendiumRecord
        {
            Id = Guid.NewGuid(),
            Name = "Record 1",
            Compendium = compendium
        };

        // Act
        var dto = record.Adapt<CompendiumRecordDto>(_config);

        // Assert
        Assert.Equal(record.Id, dto.Id);
        Assert.Equal(record.Name, dto.Name);
        Assert.Equal(compendium.Id, dto.CompendiumId);
    }
}
