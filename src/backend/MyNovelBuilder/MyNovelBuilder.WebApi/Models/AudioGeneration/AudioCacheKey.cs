using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Models.TextGeneration;

namespace MyNovelBuilder.WebApi.Models.AudioGeneration;

/// <summary>Versioned canonical fingerprints. Inputs are hashed, never persisted as JSON.</summary>
public static class AudioCacheKey
{
    /// <summary>Hashes UTF-8 text without retaining it.</summary>
    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>Hashes schema, stage and canonical inputs, sorting object properties ordinally.</summary>
    public static string Create(string stage, object inputs)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteCanonical(writer, JsonSerializer.SerializeToElement(new { schema = 1, stage, inputs }));
        }
        return Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray()));
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(writer, property.Value);
            }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item);
            writer.WriteEndArray();
        }
        else value.WriteTo(writer);
    }

    /// <summary>Identifies actual speech and consumed synthesis settings.</summary>
    public static string Synthesis(string speech, ResolvedTtsGenerationOptions options) => Create("synthesis-v1", new
    {
        speech = Hash(speech), options.Provider, options.ModelId, options.VoiceId,
        options.EndpointIdentity, options.VoiceRevision
    });

    /// <summary>Identifies emphasis input, exact prompt identity and auxiliary model.</summary>
    public static string Emphasis(string speech, ResolvedTtsGenerationOptions options, string promptIdentity) =>
        Create("emphasis-v1", new
        {
            speech = Hash(speech), promptIdentity, options.TextGenerationProvider, options.TextGenerationModelId
        });

    /// <summary>Links original speech and preparation identity to completed synthesis.</summary>
    public static string Source(string speech, ResolvedTtsGenerationOptions options, string? preparationKey) =>
        Create("source-v1", new { synthesis = Synthesis(speech, options), preparationKey });

    /// <summary>Identifies frozen planner messages, schema and auxiliary model.</summary>
    public static string ImmersivePreparation(AudiobookSectionSnapshot section, ResolvedAudiobookSettings settings,
        StructuredOutputOptions schema) => Create("immersive-preparation-v1", new
    {
        speech = Hash(section.SpeechText), messages = section.ImmersivePrompt,
        settings.TextGenerationProvider, settings.TextGenerationModelId, schema
    });

    /// <summary>Identifies planner inputs and frozen speaker assignments, excluding assembly pauses.</summary>
    public static string ImmersiveSource(AudiobookSectionSnapshot section, ResolvedAudiobookSettings settings,
        string preparationKey, string? emphasisPromptIdentity) => Create("immersive-source-v1", new
    {
        preparationKey, settings.Provider, settings.ModelId, settings.VoiceId,
        settings.TtsEndpointIdentity, settings.NarratorVoiceRevision,
        emphasisPromptIdentity,
        assignments = section.VoiceAssignments.OrderBy(a => a.CharacterRecordId)
            .Select(a => new { a.CharacterRecordId, a.VoiceId, a.VoiceRevision })
    });

    /// <summary>Identifies ordered audio, inter-chunk pause and output profile.</summary>
    public static string Section(IEnumerable<string> chunks, int pauseMs) => Create("section-v1", new
    {
        chunks, pauseMs, profile = "pcm-24000-mono-16"
    });
}
