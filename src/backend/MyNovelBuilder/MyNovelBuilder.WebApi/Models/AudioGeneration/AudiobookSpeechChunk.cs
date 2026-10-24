namespace MyNovelBuilder.WebApi.Models.AudioGeneration;

/// <summary>Resolved text-bearing speech unit; kept only in memory.</summary>
/// <param name="Text">Exact speech before optional emphasis.</param>
/// <param name="VoiceId">Resolved narrator or character voice.</param>
/// <param name="VoiceRevision">Frozen recording revision.</param>
/// <param name="IsNarratorFallback">Whether an unassigned character uses the narrator.</param>
public sealed record AudiobookSpeechChunk(string Text, string VoiceId, string? VoiceRevision, bool IsNarratorFallback);

/// <summary>A complete section artifact and its ordered speech-unit checkpoints.</summary>
/// <param name="ArtifactKey">Finite normalized WAV key.</param>
/// <param name="ChunkArtifactKeys">Ordered speech-unit keys, including duplicates.</param>
/// <param name="Reused">Whether the completed source mapping was reused.</param>
public sealed record AudiobookSectionRenderResult(string ArtifactKey, IReadOnlyList<string> ChunkArtifactKeys, bool Reused);
