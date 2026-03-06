using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.MediaLibrary;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.Errors;
using MyNovelBuilder.WebApi.Tests.Factories;
using Xunit.Abstractions;

namespace MyNovelBuilder.WebApi.Tests.Integration.Controllers;

public class MediaLibraryControllerIntegrationTests(
    TestWebApplicationFactory<Program> factory,
    ITestOutputHelper output)
    : ControllerIntegrationTests(factory, output), IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await ResetDbAsync();
    }

    [Fact]
    public async Task GetAllFolders_ReturnsLinkedFolders()
    {
        using var client = Factory.CreateClient();
        var firstPath = Directory.CreateDirectory(
            Path.Combine(StorageOptions.DataFolder, "linked-media-a")).FullName;
        var secondPath = Directory.CreateDirectory(
            Path.Combine(StorageOptions.DataFolder, "linked-media-b")).FullName;

        UnitOfWork.MediaFolders.Add(new MediaFolder
        {
            Name = "B Folder",
            Path = secondPath,
        });
        UnitOfWork.MediaFolders.Add(new MediaFolder
        {
            Name = "A Folder",
            Path = firstPath,
        });
        await UnitOfWork.SaveChangesAsync();

        var result = await GetJsonAsync<IEnumerable<MediaFolderDto>>(
            client,
            "api/media-library/folders");

        Assert.True(result.IsOk);
        var folders = result.Value.ToList();
        Assert.Equal(2, folders.Count);
        Assert.Equal("A Folder", folders[0].Name);
        Assert.Equal(firstPath, folders[0].Path);
        Assert.Equal("B Folder", folders[1].Name);
        Assert.Equal(secondPath, folders[1].Path);
    }

    [Fact]
    public async Task CreateFolder_ReturnsOk_AndPersistsNormalizedPath()
    {
        using var client = Factory.CreateClient();
        var directory = Directory.CreateDirectory(
            Path.Combine(StorageOptions.DataFolder, "linked-media-create"));
        var createDto = new CreateMediaFolderDto
        {
            Name = " Reference Media ",
            Path = Path.Combine(directory.Parent!.FullName, ".", directory.Name),
        };

        var result = await PostJsonAsync<MediaFolderDto>(
            client,
            "api/media-library/folder",
            createDto);

        Assert.True(result.IsOk);
        var dto = result.Value;
        Assert.Equal("Reference Media", dto.Name);
        Assert.Equal(directory.FullName, dto.Path);

        using var scope = Factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var storedFolder = await unitOfWork.MediaFolders.GetByIdAsync(dto.Id);
        Assert.NotNull(storedFolder);
        Assert.Equal("Reference Media", storedFolder.Name);
        Assert.Equal(directory.FullName, storedFolder.Path);
    }

    [Fact]
    public async Task CreateFolder_WithMissingPath_ReturnsBadRequest()
    {
        using var client = Factory.CreateClient();
        var missingPath = Path.Combine(StorageOptions.DataFolder, "does-not-exist");
        var createDto = new CreateMediaFolderDto
        {
            Name = "Missing Folder",
            Path = missingPath,
        };

        var result = await PostJsonAsync<MediaFolderDto>(
            client,
            "api/media-library/folder",
            createDto);

        Assert.False(result.IsOk);
        Assert.Equal(ErrorCodes.BadRequest, result.Error.Info!.Code);
        Assert.Contains("does not exist", result.Error.Info.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateFolder_WithDuplicatePath_ReturnsBadRequest()
    {
        using var client = Factory.CreateClient();
        var directory = Directory.CreateDirectory(
            Path.Combine(StorageOptions.DataFolder, "linked-media-duplicate"));

        UnitOfWork.MediaFolders.Add(new MediaFolder
        {
            Name = "Original",
            Path = directory.FullName,
        });
        await UnitOfWork.SaveChangesAsync();

        var createDto = new CreateMediaFolderDto
        {
            Name = "Duplicate",
            Path = directory.FullName,
        };

        var result = await PostJsonAsync<MediaFolderDto>(
            client,
            "api/media-library/folder",
            createDto);

        Assert.False(result.IsOk);
        Assert.Equal(ErrorCodes.BadRequest, result.Error.Info!.Code);
        Assert.Contains("already linked", result.Error.Info.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteFolder_ReturnsOk_AndRemovesFolder()
    {
        using var client = Factory.CreateClient();
        var directory = Directory.CreateDirectory(
            Path.Combine(StorageOptions.DataFolder, "linked-media-delete"));
        var mediaFolder = new MediaFolder
        {
            Name = "Delete Me",
            Path = directory.FullName,
        };
        UnitOfWork.MediaFolders.Add(mediaFolder);
        await UnitOfWork.SaveChangesAsync();

        var error = await DeleteAsync(client, $"api/media-library/folder/{mediaFolder.Id}");

        Assert.Null(error);

        using var scope = Factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var deletedFolder = await unitOfWork.MediaFolders.GetByIdAsync(mediaFolder.Id);
        Assert.Null(deletedFolder);
    }

    [Fact]
    public async Task UploadMedia_ReturnsOk_SanitizesNameAndWritesFile()
    {
        using var client = Factory.CreateClient();
        var directory = Directory.CreateDirectory(
            Path.Combine(StorageOptions.DataFolder, "linked-media-upload"));
        var mediaFolder = new MediaFolder
        {
            Name = "Upload Folder",
            Path = directory.FullName,
        };
        UnitOfWork.MediaFolders.Add(mediaFolder);
        await UnitOfWork.SaveChangesAsync();

        using var content = CreateUploadMediaFormData(
            "../Summer:Clip?.mp4",
            [0x00, 0x01, 0x02, 0x03],
            "video/mp4");

        var response = await client.PostAsync(
            $"api/media-library/folder/{mediaFolder.Id}/media",
            content);

        Assert.True(response.IsSuccessStatusCode);
        var dto = await response.Content.ReadFromJsonAsync<MediaFileDto>();
        Assert.NotNull(dto);
        Assert.Equal("Summer_Clip_.mp4", dto.FileName);
        Assert.Equal(4, dto.SizeBytes);

        var storedPath = Path.Combine(directory.FullName, "Summer_Clip_.mp4");
        Assert.True(File.Exists(storedPath));
        var storedBytes = await File.ReadAllBytesAsync(storedPath);
        Assert.Equal(new byte[] { 0x00, 0x01, 0x02, 0x03 }, storedBytes);
    }

    [Fact]
    public async Task UploadMedia_WithUnsupportedExtension_ReturnsBadRequest()
    {
        using var client = Factory.CreateClient();
        var directory = Directory.CreateDirectory(
            Path.Combine(StorageOptions.DataFolder, "linked-media-invalid-extension"));
        var mediaFolder = new MediaFolder
        {
            Name = "Invalid Extension Folder",
            Path = directory.FullName,
        };
        UnitOfWork.MediaFolders.Add(mediaFolder);
        await UnitOfWork.SaveChangesAsync();

        using var content = CreateUploadMediaFormData(
            "portrait.jpg",
            [0x01, 0x02, 0x03],
            "image/jpeg");

        var response = await client.PostAsync(
            $"api/media-library/folder/{mediaFolder.Id}/media",
            content);

        Assert.False(response.IsSuccessStatusCode);
        var responseBody = await response.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ApiError>(
            responseBody,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.BadRequest, error.Code);
        Assert.Contains(".png", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".mp4", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAllMedia_ReturnsMostRecentFirst_AndIgnoresSubfolders()
    {
        using var client = Factory.CreateClient();
        var directory = Directory.CreateDirectory(
            Path.Combine(StorageOptions.DataFolder, "linked-media-list"));
        var nestedDirectory = Directory.CreateDirectory(Path.Combine(directory.FullName, "nested"));
        var mediaFolder = new MediaFolder
        {
            Name = "List Folder",
            Path = directory.FullName,
        };
        UnitOfWork.MediaFolders.Add(mediaFolder);
        await UnitOfWork.SaveChangesAsync();

        var firstFile = Path.Combine(directory.FullName, "older.png");
        var secondFile = Path.Combine(directory.FullName, "newer.mp4");
        var nestedFile = Path.Combine(nestedDirectory.FullName, "ignored.mp4");
        await File.WriteAllBytesAsync(firstFile, [0x01]);
        await File.WriteAllBytesAsync(secondFile, [0x02, 0x03]);
        await File.WriteAllBytesAsync(nestedFile, [0x04]);
        File.SetLastWriteTimeUtc(firstFile, new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(secondFile, new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(nestedFile, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        var result = await GetJsonAsync<IEnumerable<MediaFileDto>>(
            client,
            $"api/media-library/folder/{mediaFolder.Id}/media");

        Assert.True(result.IsOk);
        var mediaFiles = result.Value.ToList();
        Assert.Equal(2, mediaFiles.Count);
        Assert.Equal("newer.mp4", mediaFiles[0].FileName);
        Assert.Equal(2, mediaFiles[0].SizeBytes);
        Assert.Equal("older.png", mediaFiles[1].FileName);
        Assert.Equal(1, mediaFiles[1].SizeBytes);
    }

    [Fact]
    public async Task GetMedia_ReturnsBytesAndMimeType()
    {
        using var client = Factory.CreateClient();
        var directory = Directory.CreateDirectory(
            Path.Combine(StorageOptions.DataFolder, "linked-media-get"));
        var mediaFolder = new MediaFolder
        {
            Name = "Get Folder",
            Path = directory.FullName,
        };
        UnitOfWork.MediaFolders.Add(mediaFolder);
        await UnitOfWork.SaveChangesAsync();

        var filePath = Path.Combine(directory.FullName, "clip.mp4");
        var bytes = new byte[] { 0x10, 0x20, 0x30 };
        await File.WriteAllBytesAsync(filePath, bytes);

        var response = await client.GetAsync($"api/media-library/folder/{mediaFolder.Id}/media/clip.mp4");

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("video/mp4", response.Content.Headers.ContentType?.MediaType);
        var returnedBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(bytes, returnedBytes);
    }

    [Fact]
    public async Task DeleteMedia_ReturnsOk_AndDeletesFile()
    {
        using var client = Factory.CreateClient();
        var directory = Directory.CreateDirectory(
            Path.Combine(StorageOptions.DataFolder, "linked-media-delete-file"));
        var mediaFolder = new MediaFolder
        {
            Name = "Delete File Folder",
            Path = directory.FullName,
        };
        UnitOfWork.MediaFolders.Add(mediaFolder);
        await UnitOfWork.SaveChangesAsync();

        var filePath = Path.Combine(directory.FullName, "scene.png");
        await File.WriteAllBytesAsync(filePath, [0x11, 0x22]);
        Assert.True(File.Exists(filePath));

        var error = await DeleteAsync(
            client,
            $"api/media-library/folder/{mediaFolder.Id}/media/scene.png");

        Assert.Null(error);
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task GetMedia_WithMissingFile_ReturnsBadRequest()
    {
        using var client = Factory.CreateClient();
        var directory = Directory.CreateDirectory(
            Path.Combine(StorageOptions.DataFolder, "linked-media-missing-file"));
        var mediaFolder = new MediaFolder
        {
            Name = "Missing File Folder",
            Path = directory.FullName,
        };
        UnitOfWork.MediaFolders.Add(mediaFolder);
        await UnitOfWork.SaveChangesAsync();

        var result = await GetJsonAsync<object>(
            client,
            $"api/media-library/folder/{mediaFolder.Id}/media/missing.png");

        Assert.False(result.IsOk);
        Assert.Equal(ErrorCodes.BadRequest, result.Error.Info!.Code);
        Assert.Contains("not found", result.Error.Info.Message, StringComparison.OrdinalIgnoreCase);
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    private static MultipartFormDataContent CreateUploadMediaFormData(
        string name,
        byte[] fileBytes,
        string contentType)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        content.Add(new StringContent(name), "name");
        content.Add(fileContent, "file", name);
        return content;
    }
}
