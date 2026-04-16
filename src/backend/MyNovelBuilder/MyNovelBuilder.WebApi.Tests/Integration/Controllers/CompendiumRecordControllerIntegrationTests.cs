using Microsoft.Extensions.DependencyInjection;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.CompendiumRecord;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Tests.Factories;
using Xunit.Abstractions;

namespace MyNovelBuilder.WebApi.Tests.Integration.Controllers;

public class CompendiumRecordControllerIntegrationTests(
    TestWebApplicationFactory<Program> factory,
    ITestOutputHelper output)
    : ControllerIntegrationTests(factory, output), IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await ResetDbAsync();
    }

    [Fact]
    public async Task GetCompendiumRecordById_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var compendium = new Compendium
        {
            Name = "Test Compendium"
        };
        UnitOfWork.Compendia.Add(compendium);
        
        var record = new CompendiumRecord
        {
            Name = "Test Record",
            Type = CompendiumRecordType.Character,
            Compendium = compendium
        };
        UnitOfWork.CompendiumRecords.Add(record);
        await UnitOfWork.SaveChangesAsync();
        
        // Act
        var result = await GetJsonAsync<CompendiumRecordDto>(
            client, $"api/compendium-record/{record.Id}");
        
        // Assert
        Assert.True(result.IsOk);
        var dto = result.Value;
        Assert.Equal(record.Name, dto.Name);
        Assert.Equal(record.Type, dto.Type);
        Assert.Equal(compendium.Id, dto.CompendiumId);
    }

    [Fact]
    public async Task GetCompendiumRecords_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var compendium = new Compendium
        {
            Name = "Test Compendium"
        };
        UnitOfWork.Compendia.Add(compendium);
        
        var record = new CompendiumRecord
        {
            Name = "Test Record",
            Type = CompendiumRecordType.Character,
            Compendium = compendium
        };
        UnitOfWork.CompendiumRecords.Add(record);
        await UnitOfWork.SaveChangesAsync();
        
        // Act
        var result = await GetJsonAsync<IEnumerable<CompendiumRecordDto>>(
            client, $"api/compendium-records?compendiumId={compendium.Id}");
        
        // Assert
        Assert.True(result.IsOk);
        var dtos = result.Value.ToList();
        Assert.Single(dtos);
        var dto = dtos[0];
        Assert.Equal(record.Name, dto.Name);
        Assert.Equal(record.Type, dto.Type);
    }

    [Fact]
    public async Task CreateCompendiumRecord_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var compendium = new Compendium
        {
            Name = "Test Compendium"
        };
        UnitOfWork.Compendia.Add(compendium);
        await UnitOfWork.SaveChangesAsync();
        
        var createDto = new CreateCompendiumRecordDto
        {
            Name = "New Record",
            Type = CompendiumRecordType.Character,
            CompendiumId = compendium.Id,
            Aliases = "Alias1, Alias2",
            Context = "Some context",
            CharacterVoiceAssignments =
            [
                new CharacterVoiceAssignmentDto
                {
                    Provider = TtsProvider.ElevenLabs,
                    ModelId = "eleven-flash-v2",
                    VoiceId = "voice-123",
                    VoiceName = "Alice",
                    UpdatedAt = new DateTime(2026, 4, 12, 10, 0, 0, DateTimeKind.Utc)
                }
            ]
        };
        
        // Act
        var result = await PostJsonAsync<CompendiumRecordDto>(
            client, "api/compendium-record", createDto);
        
        // Assert
        Assert.True(result.IsOk);
        var dto = result.Value;
        Assert.Equal(createDto.Name, dto.Name);
        Assert.Equal(createDto.Type, dto.Type);
        Assert.Equal(createDto.CompendiumId, dto.CompendiumId);
        Assert.Equal(createDto.Aliases, dto.Aliases);
        Assert.Equal(createDto.Context, dto.Context);
        var assignment = Assert.Single(dto.CharacterVoiceAssignments);
        Assert.Equal("eleven-flash-v2", assignment.ModelId);
        Assert.Equal("voice-123", assignment.VoiceId);

        using var scope = Factory.Services.CreateScope();
        var persistedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var persistedRecord = await persistedUnitOfWork.CompendiumRecords.GetByIdAsync(dto.Id);
        var persistedAssignment = Assert.Single(persistedRecord!.CharacterVoiceAssignments);
        Assert.Equal("voice-123", persistedAssignment.VoiceId);
    }
    
    [Fact]
    public async Task UpdateCompendiumRecord_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var compendium = new Compendium
        {
            Name = "Test Compendium"
        };
        UnitOfWork.Compendia.Add(compendium);
        
        var record = new CompendiumRecord
        {
            Name = "Test Record",
            Type = CompendiumRecordType.Character,
            Compendium = compendium
        };
        UnitOfWork.CompendiumRecords.Add(record);
        await UnitOfWork.SaveChangesAsync();
        
        var updateDto = new UpdateCompendiumRecordDto
        {
            Id = record.Id,
            Name = "Updated Record",
            Type = CompendiumRecordType.Object,
            Aliases = "New Alias",
            Context = "Updated context",
            CharacterVoiceAssignments =
            [
                new CharacterVoiceAssignmentDto
                {
                    Provider = TtsProvider.Kokoro,
                    ModelId = "kokoro-v1",
                    VoiceId = "kokoro-voice",
                    VoiceName = "Kira",
                    UpdatedAt = new DateTime(2026, 4, 12, 11, 0, 0, DateTimeKind.Utc)
                }
            ]
        };
        
        // Act
        var result = await PutJsonAsync<CompendiumRecordDto>(
            client, "api/compendium-record", updateDto);
        
        // Assert
        Assert.True(result.IsOk);
        var dto = result.Value;
        Assert.Equal(updateDto.Name, dto.Name);
        Assert.Equal(updateDto.Type, dto.Type);
        Assert.Equal(updateDto.Aliases, dto.Aliases);
        Assert.Equal(updateDto.Context, dto.Context);
        var assignment = Assert.Single(dto.CharacterVoiceAssignments);
        Assert.Equal("kokoro-v1", assignment.ModelId);
        Assert.Equal("kokoro-voice", assignment.VoiceId);

        using var scope = Factory.Services.CreateScope();
        var persistedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var persistedRecord = await persistedUnitOfWork.CompendiumRecords.GetByIdAsync(record.Id);
        var persistedAssignment = Assert.Single(persistedRecord!.CharacterVoiceAssignments);
        Assert.Equal(TtsProvider.Kokoro, persistedAssignment.Provider);
        Assert.Equal("kokoro-voice", persistedAssignment.VoiceId);
    }
    
    [Fact]
    public async Task DeleteCompendiumRecord_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var compendium = new Compendium
        {
            Name = "Test Compendium"
        };
        UnitOfWork.Compendia.Add(compendium);
        
        var record = new CompendiumRecord
        {
            Name = "Test Record",
            Type = CompendiumRecordType.Character,
            Compendium = compendium
        };
        UnitOfWork.CompendiumRecords.Add(record);
        await UnitOfWork.SaveChangesAsync();
        
        // Act
        var error = await DeleteAsync(
            client, $"api/compendium-record/{record.Id}");
        
        // Assert
        Assert.Null(error);
        
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var deletedRecord = await uow.CompendiumRecords.GetByIdAsync(record.Id);
        Assert.Null(deletedRecord);
    }

    [Fact]
    public async Task UploadMedia_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var compendium = new Compendium { Name = "Test Compendium" };
        UnitOfWork.Compendia.Add(compendium);
        var record = new CompendiumRecord
        {
            Name = "Test Record",
            Type = CompendiumRecordType.Character,
            Compendium = compendium
        };
        UnitOfWork.CompendiumRecords.Add(record);
        await UnitOfWork.SaveChangesAsync();

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([0x12, 0x34]);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "test.png");

        // Act
        var response = await client.PostAsync(
            $"api/compendium-record/{record.Id}/media", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Verify media was added
        var result = await GetJsonAsync<CompendiumRecordDto>(
            client, $"api/compendium-record/{record.Id}");
        Assert.True(result.IsOk);
        Assert.Single(result.Value.Media);
    }

    [Fact]
    public async Task SetCurrentImage_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var compendium = new Compendium { Name = "Test Compendium" };
        UnitOfWork.Compendia.Add(compendium);
        var record = new CompendiumRecord
        {
            Name = "Test Record",
            Type = CompendiumRecordType.Character,
            Compendium = compendium
        };
        UnitOfWork.CompendiumRecords.Add(record);
        await UnitOfWork.SaveChangesAsync();

        // Create a dummy image file so SetCurrentImage doesn't fail
        var mediaId = Guid.NewGuid();
        var galleryPath = Path.Combine(
            StorageOptions.StaticFilesRoot,
            "compendium",
            compendium.Id.ToString(),
            "records",
            record.Id.ToString(),
            "gallery");
        
        Directory.CreateDirectory(galleryPath);
        var filePath = Path.Combine(galleryPath, $"{mediaId}.png");
        await File.WriteAllBytesAsync(filePath, [0x89, 0x50, 0x4E, 0x47]); // PNG header

        // Act
        var response = await client.PostAsync(
            $"api/compendium-record/{record.Id}/image/{mediaId}/set-current", null);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Verify current image ID was updated
        var result = await GetJsonAsync<CompendiumRecordDto>(
            client, $"api/compendium-record/{record.Id}");
        Assert.True(result.IsOk);
        Assert.Equal(mediaId, result.Value.CurrentImageId);
        Assert.Contains(result.Value.Media, m => m.Id == mediaId && m.IsCurrent);
    }

    [Fact]
    public async Task DeleteMedia_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var compendium = new Compendium { Name = "Test Compendium" };
        UnitOfWork.Compendia.Add(compendium);
        var record = new CompendiumRecord
        {
            Name = "Test Record",
            Type = CompendiumRecordType.Character,
            Compendium = compendium
        };
        UnitOfWork.CompendiumRecords.Add(record);
        await UnitOfWork.SaveChangesAsync();

        // Create a dummy media file
        var mediaId = Guid.NewGuid();
        var galleryPath = Path.Combine(
            StorageOptions.StaticFilesRoot,
            "compendium",
            compendium.Id.ToString(),
            "records",
            record.Id.ToString(),
            "gallery");
        
        Directory.CreateDirectory(galleryPath);
        var filePath = Path.Combine(galleryPath, $"{mediaId}.png");
        await File.WriteAllBytesAsync(filePath, [0x12, 0x34]);

        // Act
        var response = await client.DeleteAsync(
            $"api/compendium-record/{record.Id}/media/{mediaId}");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.False(File.Exists(filePath));

        // Verify media list is empty
        var result = await GetJsonAsync<CompendiumRecordDto>(
            client, $"api/compendium-record/{record.Id}");
        Assert.True(result.IsOk);
        Assert.Empty(result.Value.Media);
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
