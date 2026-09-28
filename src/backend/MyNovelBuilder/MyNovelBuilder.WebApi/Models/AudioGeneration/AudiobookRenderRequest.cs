namespace MyNovelBuilder.WebApi.Models.AudioGeneration;

/// <summary>Internal rendering contract: settings and section inputs travel together.</summary>
public sealed record AudiobookRenderRequest(
    Guid NovelId,
    int ChapterIndex,
    ResolvedAudiobookSettings Settings,
    AudiobookSectionSnapshot Section);
