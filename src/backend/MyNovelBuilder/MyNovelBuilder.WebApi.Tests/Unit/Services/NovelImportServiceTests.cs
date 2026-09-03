using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Services;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public class NovelImportServiceTests
{
    private readonly NovelImportService _service = new();

    [Fact]
    public void ImportFromMarkdown_ParsesExportedNovelStructure()
    {
        const string markdown = """
                                # The Novel
                                by Jane Writer

                                A short brief.

                                ## Chapter One
                                First paragraph.

                                **Second paragraph.**

                                ## Chapter Two
                                Final paragraph.
                                """;

        var result = _service.ImportFromMarkdown(markdown);

        Assert.Collection(
            result.Chapters,
            chapter =>
            {
                Assert.Equal("Chapter One", chapter.Title);
                var section = Assert.Single(chapter.Sections);
                Assert.Contains("<p>First paragraph.</p>", section.Text);
                Assert.Contains("<strong>Second paragraph.</strong>", section.Text);
            },
            chapter =>
            {
                Assert.Equal("Chapter Two", chapter.Title);
                Assert.Contains("Final paragraph.", Assert.Single(chapter.Sections).Text);
            });
    }

    [Fact]
    public void ImportFromMarkdown_IgnoresHeadingsInsideCodeFences()
    {
        const string markdown = """
                                # Code Story

                                ## Real Chapter
                                ```markdown
                                ## This is code
                                ```
                                The end.
                                """;

        var result = _service.ImportFromMarkdown(markdown);

        var chapter = Assert.Single(result.Chapters);
        Assert.Equal("Real Chapter", chapter.Title);
        Assert.Contains("## This is code", Assert.Single(chapter.Sections).Text);
    }

    [Fact]
    public void ImportFromMarkdown_AllowsAnEmptyNovelExport()
    {
        var result = _service.ImportFromMarkdown("# Empty Novel");

        Assert.Empty(result.Chapters);
    }

    [Fact]
    public void ImportFromMarkdown_RejectsDocumentWithoutTitle()
    {
        var exception = Assert.Throws<ApiException>(() =>
            _service.ImportFromMarkdown("## Chapter One\nText"));

        Assert.Equal(ErrorCodes.InvalidFile, exception.Code);
        Assert.Contains("level-one title", exception.Message);
    }

}
