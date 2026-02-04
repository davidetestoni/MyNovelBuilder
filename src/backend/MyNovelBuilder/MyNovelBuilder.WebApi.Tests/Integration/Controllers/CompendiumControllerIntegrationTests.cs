using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Compendium;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Tests.Factories;
using Xunit.Abstractions;

namespace MyNovelBuilder.WebApi.Tests.Integration.Controllers;

public class CompendiumControllerIntegrationTests(
    TestWebApplicationFactory<Program> factory,
    ITestOutputHelper output)
    : ControllerIntegrationTests(factory, output), IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await ResetDbAsync();
    }

    [Fact]
    public async Task GetCompendium_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var compendium = new Compendium
        {
            Name = "Test Compendium",
            Description = "A compendium for testing."
        };
        UnitOfWork.Compendia.Add(compendium);
        await UnitOfWork.SaveChangesAsync();
        
        // Act
        var result = await GetJsonAsync<CompendiumDto>(
            client, $"api/compendium/{compendium.Id}");
        
        // Assert
        Assert.True(result.IsOk);
        var dto = result.Value;
        Assert.Equal(compendium.Name, dto.Name);
        Assert.Equal(compendium.Description, dto.Description);
        Assert.Empty(dto.Records);
    }

    [Fact]
    public async Task GetCompendium_WithRecords_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var record = new CompendiumRecord
        {
            Name = "Test Record",
            Type = CompendiumRecordType.Character
        };
        UnitOfWork.CompendiumRecords.Add(record);
        var compendium = new Compendium
        {
            Name = "Test Compendium",
            Description = "A compendium for testing.",
            Records = [record]
        };
        UnitOfWork.Compendia.Add(compendium);
        await UnitOfWork.SaveChangesAsync();
        
        // Act
        var result = await GetJsonAsync<CompendiumDto>(
            client, $"api/compendium/{compendium.Id}");
        
        // Assert
        Assert.True(result.IsOk);
        var dto = result.Value;
        Assert.Equal(compendium.Name, dto.Name);
        Assert.Equal(compendium.Description, dto.Description);
        var recordsDto = dto.Records.ToList();
        Assert.Single(recordsDto);
        var recordDto = recordsDto[0];
        Assert.Equal(record.Name, recordDto.Name);
        Assert.Equal(record.Type, recordDto.Type);
    }

    [Fact]
    public async Task GetAllCompendia_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var compendium = new Compendium
        {
            Name = "Test Compendium",
            Description = "A compendium for testing."
        };
        UnitOfWork.Compendia.Add(compendium);
        await UnitOfWork.SaveChangesAsync();
        
        // Act
        var result = await GetJsonAsync<IEnumerable<CompendiumDto>>(
            client, "api/compendia");
        
        // Assert
        Assert.True(result.IsOk);
        var dtos = result.Value.ToList();
        Assert.Single(dtos);
        var dto = dtos[0];
        Assert.Equal(compendium.Name, dto.Name);
        Assert.Equal(compendium.Description, dto.Description);
        Assert.Empty(compendium.Records);
    }

    [Fact]
    public async Task CreateCompendium_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var createDto = new CreateCompendiumDto
        {
            Name = "New Compendium",
            Description = "A newly created compendium."
        };
        
        // Act
        var result = await PostJsonAsync<CompendiumDto>(
            client, "api/compendium", createDto);
        
        // Assert
        Assert.True(result.IsOk);
        var dto = result.Value;
        Assert.Equal(createDto.Name, dto.Name);
        Assert.Equal(createDto.Description, dto.Description);
        Assert.Empty(dto.Records);
    }
    
    [Fact]
    public async Task UpdateCompendium_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var compendium = new Compendium
        {
            Name = "Test Compendium",
            Description = "A compendium for testing."
        };
        UnitOfWork.Compendia.Add(compendium);
        await UnitOfWork.SaveChangesAsync();
        
        var updateDto = new UpdateCompendiumDto
        {
            Id = compendium.Id,
            Name = "Updated Compendium",
            Description = "An updated compendium."
        };
        
        // Act
        var result = await PutJsonAsync<CompendiumDto>(
            client, "api/compendium", updateDto);
        
        // Assert
        Assert.True(result.IsOk);
        var dto = result.Value;
        Assert.Equal(updateDto.Name, dto.Name);
        Assert.Equal(updateDto.Description, dto.Description);
        Assert.Empty(dto.Records);
    }
    
    [Fact]
    public async Task DeleteCompendium_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var compendium = new Compendium
        {
            Name = "Test Compendium",
            Description = "A compendium for testing."
        };
        UnitOfWork.Compendia.Add(compendium);
        await UnitOfWork.SaveChangesAsync();
        
        // Act
        var error = await DeleteAsync(
            client, $"api/compendium/{compendium.Id}");
        
        // Assert
        Assert.Null(error);
    }
    
    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
