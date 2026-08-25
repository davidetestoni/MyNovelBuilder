using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Voice;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.Errors;
using MyNovelBuilder.WebApi.Tests.Factories;
using NAudio.Wave;
using Xunit.Abstractions;

namespace MyNovelBuilder.WebApi.Tests.Integration.Controllers;

public class VoiceControllerIntegrationTests(
    TestWebApplicationFactory<Program> factory,
    ITestOutputHelper output)
    : ControllerIntegrationTests(factory, output), IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await ResetDbAsync();
    }

    [Fact]
    public async Task GetAllVoices_ReturnsSeededVoices()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var firstVoice = new Voice
        {
            Name = "Narrator One",
            VoiceGender = VoiceGender.Female,
            Language = WritingLanguage.English
        };
        var secondVoice = new Voice
        {
            Name = "Narrator Two",
            VoiceGender = VoiceGender.Male,
            Language = WritingLanguage.German
        };
        UnitOfWork.Voices.Add(firstVoice);
        UnitOfWork.Voices.Add(secondVoice);
        await UnitOfWork.SaveChangesAsync();

        // Act
        var result = await GetJsonAsync<IEnumerable<VoiceDto>>(client, "api/voices");

        // Assert
        Assert.True(result.IsOk);
        var voices = result.Value.ToList();
        Assert.Equal(2, voices.Count);
        Assert.Contains(
            voices,
            v => v.Id == firstVoice.Id
                 && v.Name == firstVoice.Name
                 && v.VoiceGender == firstVoice.VoiceGender
                 && v.Language == firstVoice.Language);
        Assert.Contains(
            voices,
            v => v.Id == secondVoice.Id
                 && v.Name == secondVoice.Name
                 && v.VoiceGender == secondVoice.VoiceGender
                 && v.Language == secondVoice.Language);
    }

    [Fact]
    public async Task CreateVoice_ReturnsOk_PersistsVoiceAndWavFile()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var wavBytes = CreateWavBytes(seconds: 1);
        using var content = CreateVoiceFormData(
            name: "New Voice",
            voiceGender: VoiceGender.Both,
            language: WritingLanguage.Spanish,
            transcript: "Estas son las palabras exactas.",
            fileName: "voice.wav",
            fileBytes: wavBytes);

        // Act
        var response = await client.PostAsync("api/voices", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var createdVoice = dbContext.Voices.Single(v => v.Name == "New Voice");
        Assert.Equal(VoiceGender.Both, createdVoice.VoiceGender);
        Assert.Equal(WritingLanguage.Spanish, createdVoice.Language);
        Assert.Equal("Estas son las palabras exactas.", createdVoice.Transcript);

        var wavPath = GetVoiceWavPath(createdVoice.Id);
        Assert.True(File.Exists(wavPath));
        var storedBytes = await File.ReadAllBytesAsync(wavPath);
        Assert.Equal(wavBytes, storedBytes);
    }

    [Fact]
    public async Task UpdateVoice_ReturnsOk_UpdatesVoiceAndReplacesWavFile()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var voice = new Voice
        {
            Name = "Original Voice",
            VoiceGender = VoiceGender.Male,
            Language = WritingLanguage.English
        };
        UnitOfWork.Voices.Add(voice);
        await UnitOfWork.SaveChangesAsync();

        var originalBytes = CreateWavBytes(seconds: 1);
        var wavPath = GetVoiceWavPath(voice.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(wavPath)!);
        await File.WriteAllBytesAsync(wavPath, originalBytes);

        var updatedBytes = CreateWavBytes(seconds: 2);
        using var content = CreateVoiceFormData(
            name: "Updated Voice",
            voiceGender: VoiceGender.Female,
            language: WritingLanguage.French,
            transcript: "Voici les mots exacts.",
            fileName: "updated.wav",
            fileBytes: updatedBytes,
            id: voice.Id);

        // Act
        var response = await client.PutAsync("api/voices", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updatedVoice = dbContext.Voices.Single(v => v.Id == voice.Id);
        Assert.Equal("Updated Voice", updatedVoice.Name);
        Assert.Equal(VoiceGender.Female, updatedVoice.VoiceGender);
        Assert.Equal(WritingLanguage.French, updatedVoice.Language);
        Assert.Equal("Voici les mots exacts.", updatedVoice.Transcript);

        var storedBytes = await File.ReadAllBytesAsync(wavPath);
        Assert.Equal(updatedBytes, storedBytes);
    }

    [Fact]
    public async Task UpdateVoice_WithoutFile_UpdatesMetadataAndPreservesWavFile()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var voice = new Voice
        {
            Name = "Original Voice",
            VoiceGender = VoiceGender.Male,
            Language = WritingLanguage.English,
            Transcript = "Original transcript"
        };
        UnitOfWork.Voices.Add(voice);
        await UnitOfWork.SaveChangesAsync();

        var originalBytes = CreateWavBytes(seconds: 1);
        var wavPath = GetVoiceWavPath(voice.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(wavPath)!);
        await File.WriteAllBytesAsync(wavPath, originalBytes);

        using var content = CreateVoiceFormData(
            name: "Metadata Only",
            voiceGender: VoiceGender.Female,
            language: WritingLanguage.Italian,
            transcript: "Updated transcript",
            fileName: "unused.wav",
            fileBytes: [],
            id: voice.Id,
            includeFile: false);

        // Act
        var response = await client.PutAsync("api/voices", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updatedVoice = dbContext.Voices.Single(v => v.Id == voice.Id);
        Assert.Equal("Metadata Only", updatedVoice.Name);
        Assert.Equal(VoiceGender.Female, updatedVoice.VoiceGender);
        Assert.Equal(WritingLanguage.Italian, updatedVoice.Language);
        Assert.Equal("Updated transcript", updatedVoice.Transcript);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(wavPath));
    }

    [Fact]
    public async Task DeleteVoice_ReturnsOk_RemovesVoiceAndWavFile()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var voice = new Voice
        {
            Name = "Voice To Delete",
            VoiceGender = VoiceGender.Both,
            Language = WritingLanguage.English
        };
        UnitOfWork.Voices.Add(voice);
        await UnitOfWork.SaveChangesAsync();

        var wavPath = GetVoiceWavPath(voice.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(wavPath)!);
        await File.WriteAllBytesAsync(wavPath, CreateWavBytes(seconds: 1));
        Assert.True(File.Exists(wavPath));

        // Act
        var error = await DeleteAsync(client, $"api/voices/{voice.Id}");

        // Assert
        Assert.Null(error);

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deletedVoice = await dbContext.Voices.FindAsync(voice.Id);
        Assert.Null(deletedVoice);
        Assert.False(File.Exists(wavPath));
    }

    [Fact]
    public async Task GetVoicePreview_ReturnsWavFile()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var voice = new Voice
        {
            Name = "Preview Voice",
            VoiceGender = VoiceGender.Both,
            Language = WritingLanguage.English
        };
        UnitOfWork.Voices.Add(voice);
        await UnitOfWork.SaveChangesAsync();

        var fullWav = CreateWavBytes(seconds: 2);
        var wavPath = GetVoiceWavPath(voice.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(wavPath)!);
        await File.WriteAllBytesAsync(wavPath, fullWav);

        // Act
        var response = await client.GetAsync($"api/voices/{voice.Id}/preview?seconds=1");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("audio/wav", response.Content.Headers.ContentType?.MediaType);
        var previewBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(previewBytes.Length >= 44);
        Assert.True(previewBytes.Length < fullWav.Length);
    }

    [Fact]
    public async Task CreateVoice_WithNonWavFile_ReturnsBadRequest()
    {
        // Arrange
        using var client = Factory.CreateClient();
        using var content = CreateVoiceFormData(
            name: "Invalid File Voice",
            voiceGender: VoiceGender.Both,
            language: WritingLanguage.English,
            fileName: "voice.mp3",
            fileBytes: [0x01, 0x02, 0x03],
            contentType: "audio/mpeg");

        // Act
        var response = await client.PostAsync("api/voices", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await DeserializeApiErrorAsync(response);
        Assert.Equal(ErrorCodes.BadRequest, error.Code);
        Assert.Contains(".wav", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateVoice_WithUnknownId_ReturnsVoiceNotFound()
    {
        // Arrange
        using var client = Factory.CreateClient();
        using var content = CreateVoiceFormData(
            name: "Missing Voice",
            voiceGender: VoiceGender.Both,
            language: WritingLanguage.English,
            fileName: "voice.wav",
            fileBytes: CreateWavBytes(seconds: 1),
            id: Guid.NewGuid());

        // Act
        var response = await client.PutAsync("api/voices", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await DeserializeApiErrorAsync(response);
        Assert.Equal(ErrorCodes.VoiceNotFound, error.Code);
    }

    [Fact]
    public async Task GetVoicePreview_WithInvalidSeconds_ReturnsBadRequest()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var voice = new Voice
        {
            Name = "Voice For Invalid Preview",
            VoiceGender = VoiceGender.Both,
            Language = WritingLanguage.English
        };
        UnitOfWork.Voices.Add(voice);
        await UnitOfWork.SaveChangesAsync();

        // Act
        var result = await GetJsonAsync<object>(
            client, $"api/voices/{voice.Id}/preview?seconds=0");

        // Assert
        Assert.NotNull(result.Error);
        Assert.Equal(HttpStatusCode.BadRequest, result.Error.Response.StatusCode);
        Assert.NotNull(result.Error.Info);
        Assert.Equal(ErrorCodes.BadRequest, result.Error.Info!.Code);
    }

    [Fact]
    public async Task GetVoicePreview_WithUnknownId_ReturnsVoiceNotFound()
    {
        // Arrange
        using var client = Factory.CreateClient();

        // Act
        var result = await GetJsonAsync<object>(
            client, $"api/voices/{Guid.NewGuid()}/preview?seconds=1");

        // Assert
        Assert.NotNull(result.Error);
        Assert.Equal(HttpStatusCode.BadRequest, result.Error.Response.StatusCode);
        Assert.NotNull(result.Error.Info);
        Assert.Equal(ErrorCodes.VoiceNotFound, result.Error.Info!.Code);
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    private static MultipartFormDataContent CreateVoiceFormData(
        string name,
        VoiceGender voiceGender,
        WritingLanguage language,
        string fileName,
        byte[] fileBytes,
        string contentType = "audio/wav",
        Guid? id = null,
        string? transcript = null,
        bool includeFile = true)
    {
        var content = new MultipartFormDataContent();

        if (id.HasValue)
        {
            content.Add(new StringContent(id.Value.ToString()), "id");
        }

        content.Add(new StringContent(name), "name");
        content.Add(new StringContent(voiceGender.ToString()), "voiceGender");
        content.Add(new StringContent(language.ToString()), "language");

        if (transcript is not null)
        {
            content.Add(new StringContent(transcript), "transcript");
        }

        if (includeFile)
        {
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            content.Add(fileContent, "file", fileName);
        }

        return content;
    }

    private string GetVoiceWavPath(Guid id)
    {
        return Path.Combine(StorageOptions.DataFolder, "voices", $"{id}.wav");
    }

    private static async Task<ApiError> DeserializeApiErrorAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Deserialize<ApiError>(json, options)!;
    }

    private static byte[] CreateWavBytes(int seconds)
    {
        const int sampleRate = 8000;
        const int channels = 1;
        const int bitsPerSample = 16;
        var bytesPerSample = bitsPerSample / 8;
        var sampleData = new byte[sampleRate * seconds * channels * bytesPerSample];

        using var stream = new MemoryStream();
        using (var writer = new WaveFileWriter(stream, new WaveFormat(sampleRate, bitsPerSample, channels)))
        {
            writer.Write(sampleData, 0, sampleData.Length);
        }

        return stream.ToArray();
    }
}
