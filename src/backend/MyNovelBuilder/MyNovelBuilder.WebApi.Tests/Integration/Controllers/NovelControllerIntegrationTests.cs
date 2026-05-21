using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Novel;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.Novels;
using MyNovelBuilder.WebApi.Options;
using MyNovelBuilder.WebApi.Tests.Factories;
using Xunit.Abstractions;

namespace MyNovelBuilder.WebApi.Tests.Integration.Controllers;

public class NovelControllerIntegrationTests(
    TestWebApplicationFactory<Program> factory,
    ITestOutputHelper output)
    : ControllerIntegrationTests(factory, output), IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await ResetDbAsync();
    }

    [Fact]
    public async Task GetNovelById_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var novel = new Novel
        {
            Title = "Test Novel",
            Author = "Test Author",
            Brief = "Test Brief"
        };
        UnitOfWork.Novels.Add(novel);
        await UnitOfWork.SaveChangesAsync();

        // Act
        var result = await GetJsonAsync<NovelDto>(
            client, $"api/novel/{novel.Id}");

        // Assert
        Assert.True(result.IsOk);
        Assert.Equal(novel.Id, result.Value.Id);
        Assert.Equal(novel.Title, result.Value.Title);
    }

    [Fact]
    public async Task GetNovelProse_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var novelId = Guid.NewGuid();
        var novel = new Novel
        {
            Id = novelId,
            Title = "Test Novel",
            Author = "Author",
            Brief = "Brief"
        };
        UnitOfWork.Novels.Add(novel);
        await UnitOfWork.SaveChangesAsync();

        // Act
        var result = await GetJsonAsync<Prose>(
            client, $"api/novel/{novelId}/prose");

        // Assert
        Assert.True(result.IsOk);
        var prose = result.Value;
        Assert.NotNull(prose);
        Assert.Empty(prose.Chapters);
    }

    [Fact]
    public async Task GetAllNovels_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var novel = new Novel
        {
            Title = "Test Novel",
            Author = "Author",
            Brief = "Brief"
        };
        UnitOfWork.Novels.Add(novel);
        await UnitOfWork.SaveChangesAsync();

        // Act
        var result = await GetJsonAsync<IEnumerable<NovelDto>>(
            client, "api/novels");

        // Assert
        Assert.True(result.IsOk);
        var novels = result.Value.ToList();
        Assert.Single(novels);
        Assert.Equal(novel.Title, novels[0].Title);
    }

    [Fact]
    public async Task CreateNovel_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var createDto = new CreateNovelDto
        {
            Title = "New Novel",
            Author = "New Author",
            Brief = "New Brief",
            Tense = WritingTense.Past,
            Pov = WritingPov.FirstPerson,
            Language = WritingLanguage.English
        };

        // Act
        var result = await PostJsonAsync<NovelDto>(
            client, "api/novel", createDto);

        // Assert
        Assert.True(result.IsOk);
        var novelDto = result.Value;
        Assert.Equal(createDto.Title, novelDto.Title);
        Assert.Equal(createDto.Author, novelDto.Author);
    }

    [Fact]
    public async Task UpdateNovel_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var novel = new Novel
        {
            Title = "Original Title",
            Author = "Author",
            Brief = "Brief"
        };
        UnitOfWork.Novels.Add(novel);
        await UnitOfWork.SaveChangesAsync();

        var updateDto = new UpdateNovelDto
        {
            Id = novel.Id,
            Title = "Updated Title",
            Author = "Updated Author",
            Brief = "Updated Brief",
            Tense = WritingTense.Present,
            Pov = WritingPov.FirstPerson,
            Language = WritingLanguage.English,
            CompendiumIds = []
        };

        // Act
        var result = await PutJsonAsync<NovelDto>(
            client, "api/novel", updateDto);

        // Assert
        Assert.True(result.IsOk);
        Assert.Equal(updateDto.Title, result.Value.Title);
        Assert.Equal(updateDto.Author, result.Value.Author);
    }

    [Fact]
    public async Task UpdateNovelProse_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var novel = new Novel
        {
            Title = "Novel for Prose Update",
            Author = "Author",
            Brief = "Brief"
        };
        UnitOfWork.Novels.Add(novel);
        await UnitOfWork.SaveChangesAsync();

        var prose = new Prose
        {
            Chapters = new List<Chapter>
            {
                new()
                {
                    Title = "Chapter 1",
                    Sections = new List<Section>
                    {
                        new() { Text = "Section text" }
                    }
                }
            }
        };

        // Act
        var result = await PutJsonAsync<object>(
            client, $"api/novel/{novel.Id}/prose", prose);

        // Assert
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task DeleteNovel_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var novel = new Novel
        {
            Title = "Novel to Delete",
            Author = "Author",
            Brief = "Brief"
        };
        UnitOfWork.Novels.Add(novel);
        await UnitOfWork.SaveChangesAsync();

        var prose = new Prose
        {
            Chapters =
            [
                new Chapter
                {
                    Title = "Chapter 1",
                    Sections =
                    [
                        new Section { Text = "Section text" }
                    ]
                }
            ]
        };
        await PutJsonAsync<object>(client, $"api/novel/{novel.Id}/prose", prose);

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        content.Add(fileContent, "file", "cover.png");
        var uploadResponse = await client.PostAsync($"api/novel/{novel.Id}/cover-image", content);
        Assert.True(uploadResponse.IsSuccessStatusCode);

        var prosePath = Path.Combine(StorageOptions.DataFolder, "novels", novel.Id.ToString(), "prose.json");
        var staticNovelFolder = Path.Combine(StorageOptions.StaticFilesRoot, "novels", novel.Id.ToString());
        Assert.True(File.Exists(prosePath));
        Assert.True(Directory.Exists(staticNovelFolder));

        // Act
        var error = await DeleteAsync(
            client, $"api/novel/{novel.Id}");

        // Assert
        Assert.Null(error);
        
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deletedNovel = await dbContext.Novels.FindAsync(novel.Id);
        Assert.Null(deletedNovel);
        Assert.False(File.Exists(prosePath));
        Assert.False(Directory.Exists(staticNovelFolder));
    }

    [Fact]
    public async Task UploadCoverImage_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var novel = new Novel
        {
            Title = "Novel for Cover",
            Author = "Author",
            Brief = "Brief"
        };
        UnitOfWork.Novels.Add(novel);
        await UnitOfWork.SaveChangesAsync();

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]); // Fake PNG header
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        content.Add(fileContent, "file", "cover.png");

        // Act
        var response = await client.PostAsync(
            $"api/novel/{novel.Id}/cover-image", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task UploadProseImage_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var novel = new Novel
        {
            Title = "Novel for Prose Image",
            Author = "Author",
            Brief = "Brief"
        };
        UnitOfWork.Novels.Add(novel);
        await UnitOfWork.SaveChangesAsync();

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]); // Fake PNG header
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        content.Add(fileContent, "file", "prose.png");

        // Act
        var response = await client.PostAsync($"api/novel/{novel.Id}/prose-image", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(json);
        var location = JsonSerializer.Deserialize<string>(json);
        Assert.False(string.IsNullOrWhiteSpace(location));
    }

    [Fact]
    public async Task UploadProseImage_WhenMp4_ReturnsOk_AndKeepsVideoExtension()
    {
        using var client = Factory.CreateClient();
        var novel = new Novel
        {
            Title = "Novel for Prose Video",
            Author = "Author",
            Brief = "Brief"
        };
        UnitOfWork.Novels.Add(novel);
        await UnitOfWork.SaveChangesAsync();

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70]);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("video/mp4");
        content.Add(fileContent, "file", "prose.mp4");

        var response = await client.PostAsync($"api/novel/{novel.Id}/prose-image", content);

        Assert.True(response.IsSuccessStatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var location = JsonSerializer.Deserialize<string>(json);
        Assert.False(string.IsNullOrWhiteSpace(location));
        Assert.EndsWith(".mp4", location, StringComparison.OrdinalIgnoreCase);

        using var scope = Factory.Services.CreateScope();
        var storageOptions = scope.ServiceProvider.GetRequiredService<IOptions<AppStorageOptions>>().Value;
        var filePath = Path.Combine(storageOptions.StaticFilesRoot, "novels", novel.Id.ToString(), "prose-images", location!);
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task DeleteProseImage_ReturnsOk_AndDeletesFile()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var novel = new Novel
        {
            Title = "Novel for Prose Image Deletion",
            Author = "Author",
            Brief = "Brief"
        };
        UnitOfWork.Novels.Add(novel);
        await UnitOfWork.SaveChangesAsync();

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        content.Add(fileContent, "file", "prose.png");

        var uploadResponse = await client.PostAsync($"api/novel/{novel.Id}/prose-image", content);
        Assert.True(uploadResponse.IsSuccessStatusCode);

        var uploadJson = await uploadResponse.Content.ReadAsStringAsync();
        var location = JsonSerializer.Deserialize<string>(uploadJson);
        Assert.False(string.IsNullOrWhiteSpace(location));

        using var scope = Factory.Services.CreateScope();
        var storageOptions = scope.ServiceProvider.GetRequiredService<IOptions<AppStorageOptions>>().Value;
        var filePath = Path.Combine(storageOptions.StaticFilesRoot, "novels", novel.Id.ToString(), "prose-images", location!);
        Assert.True(File.Exists(filePath));

        // Act
        var deleteResponse = await client.DeleteAsync($"api/novel/{novel.Id}/prose-image/{Uri.EscapeDataString(location!)}");

        // Assert
        Assert.True(deleteResponse.IsSuccessStatusCode);
        Assert.False(File.Exists(filePath));
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
