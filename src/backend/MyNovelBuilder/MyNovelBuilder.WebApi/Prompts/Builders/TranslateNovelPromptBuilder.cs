using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Extensions;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// Prompt builder for translating a chapter of a novel.
/// </summary>
public partial class TranslateNovelPromptBuilder : NovelPromptBuilder<TranslateNovelContextInfoDto>
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="TranslateNovelPromptBuilder"/> class.
    /// </summary>
    public TranslateNovelPromptBuilder(string prompt) : base(prompt)
    {
    }

    /// <inheritdoc />
    public override NovelPromptBuilder<TranslateNovelContextInfoDto> ReplacePlaceholders(
        NovelPromptBuilderContext<TranslateNovelContextInfoDto> context)
    {
        base.ReplacePlaceholders(context);

        var chapter = PromptBuilderUtils.GetChapter(context.Prose, context.Client.ChapterIndex);
        var sections = chapter.Sections.ToList();

        var textForFiltering = string.Join(
            "\n\n",
            sections.Select(s => $"{s.Summary}\n{s.Text.StripHtml()}"));
        var recordsInContext = PromptBuilderUtils.FilterRecordsInContext(
            context.CompendiumRecords,
            textForFiltering);
        if (!string.IsNullOrWhiteSpace(context.Client.Instructions))
        {
            recordsInContext.UnionWith(
                PromptBuilderUtils.FilterRecordsInContext(
                    context.CompendiumRecords,
                    context.Client.Instructions));
        }

        var recordsInContextList = recordsInContext.ToList();
        PromptBuilderUtils.TrackIncludedRecords(
            context.IncludedCompendiumRecordIds,
            recordsInContextList);

        Builder
            .Replace("{{targetLanguage}}", context.Client.TargetLanguage.ToString())
            .Replace("{{context}}", SerializeChapter(chapter))
            .Replace("{{instructions}}", context.Client.Instructions ?? string.Empty)
            .Replace("{{records}}", PromptBuilderUtils.CreateCompendiumRecordsString(
                recordsInContextList,
                (
                    context.Prose,
                    context.Client.ChapterIndex,
                    null
                )));

        return this;
    }

    private static string SerializeChapter(Models.Novels.Chapter chapter)
    {
        return JsonSerializer.Serialize(new
        {
            chapterTitle = NormalizeTextForPrompt(chapter.Title),
            storyEvents = chapter.StoryEvents.Select(e => new
            {
                title = NormalizeTextForPrompt(e.Title),
                date = NormalizeTextForPrompt(e.Date),
                description = NormalizeTextForPrompt(e.Description)
            }),
            sections = chapter.Sections.Select((section, index) => new
            {
                sectionIndex = index,
                summary = NormalizeTextForPrompt(section.Summary),
                text = NormalizeHtmlForPrompt(section.Text)
            })
        }, _jsonSerializerOptions);
    }

    private static string NormalizeTextForPrompt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return System.Net.WebUtility.HtmlDecode(text)
            .Replace('\u00A0', ' ');
    }

    private static string NormalizeHtmlForPrompt(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var decoded = NormalizeTextForPrompt(html);

        // Inline base64 image data wastes a large amount of tokens and is not useful for translation.
        return InlineBase64ImageRegex().Replace(decoded, "<img alt=\"embedded image omitted\" />");
    }

    [GeneratedRegex("""<img\b[^>]*\bsrc\s*=\s*["']data:image\/[^"']*["'][^>]*>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex InlineBase64ImageRegex();
}
