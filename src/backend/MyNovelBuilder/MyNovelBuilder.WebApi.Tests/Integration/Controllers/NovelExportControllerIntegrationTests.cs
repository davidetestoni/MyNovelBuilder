using System.Text;
using Microsoft.Extensions.DependencyInjection;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Models.Novels;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Tests.Factories;
using Xunit.Abstractions;

namespace MyNovelBuilder.WebApi.Tests.Integration.Controllers;

public class NovelExportControllerIntegrationTests(
    TestWebApplicationFactory<Program> factory,
    ITestOutputHelper output)
    : ControllerIntegrationTests(factory, output), IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await ResetDbAsync();
    }

    [Fact]
    public async Task ExportToMarkdown_ReturnsOk()
    {
        using var client = Factory.CreateClient();
        var novelId = await CreateNovelWithProseAsync();

        var response = await client.GetAsync($"api/novel/{novelId}/export/markdown");

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("text/markdown", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("test_novel.md", response.Content.Headers.ContentDisposition?.FileName);

        var result = await response.Content.ReadAsStringAsync();
        const string expected = """
                                # Test Novel
                                by Test Author

                                Test Brief

                                ## Chapter 1
                                Section 1 text

                                Section 2 text

                                ## Chapter 2
                                Section 3 text
                                """;
        
        Assert.Equal(
            expected.Replace("\r\n", "\n").Trim(),
            result.Replace("\r\n", "\n").Trim());
    }

    [Fact]
    public async Task ExportToHtml_ReturnsOk()
    {
        using var client = Factory.CreateClient();
        var novelId = await CreateNovelWithProseAsync();

        var response = await client.GetAsync($"api/novel/{novelId}/export/html");

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("test_novel.html", response.Content.Headers.ContentDisposition?.FileName);

        var result = await response.Content.ReadAsStringAsync();
        Assert.Contains("<h1>Test Novel</h1>", result);
        Assert.Contains("<h2>Chapter 1</h2>", result);
        Assert.Contains("<p>Section 1 text</p>", result);
    }

    [Fact]
    public async Task ExportToPdf_ReturnsOk()
    {
        using var client = Factory.CreateClient();
        var novelId = await CreateNovelWithProseAsync();

        var response = await client.GetAsync($"api/novel/{novelId}/export/pdf");

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("test_novel.pdf", response.Content.Headers.ContentDisposition?.FileName);

        var result = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(result);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(result.Take(4).ToArray()));
    }

    private async Task<Guid> CreateNovelWithProseAsync()
    {
        var novelId = Guid.NewGuid();
        var novel = new Novel
        {
            Id = novelId,
            Title = "Test Novel",
            Author = "Test Author",
            Brief = "Test Brief"
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
                        new() { Text = "<p>Section 1 text</p>" },
                        new() { Text = "<p>Section 2 text</p>" }
                    }
                },
                new()
                {
                    Title = "Chapter 2",
                    Sections = new List<Section>
                    {
                        new() { Text = "<p>Section 3 text</p>" }
                    }
                }
            }
        };

        using var scope = Factory.Services.CreateScope();
        var novelService = scope.ServiceProvider.GetRequiredService<INovelService>();
        await novelService.UpdateProseAsync(novelId, prose);

        return novelId;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
