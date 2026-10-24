using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using MyNovelBuilder.WebApi.Models.AudioGeneration;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>Text-bearing preparations live exactly as long as the in-memory section snapshot.</summary>
public sealed class AudiobookPreparationCache
{
    private readonly ConditionalWeakTable<AudiobookSectionSnapshot, ConcurrentDictionary<string, object>> _sections = new();

    /// <summary>Gets a successful preparation without storing failed/cancelled attempts.</summary>
    public async Task<T> GetOrCreateAsync<T>(AudiobookSectionSnapshot? section, string key, Func<Task<T>> prepare) where T : notnull
    {
        if (section is null) return await prepare();
        var values = _sections.GetOrCreateValue(section);
        if (values.TryGetValue(key, out var value)) return (T)value;
        var result = await prepare();
        values.TryAdd(key, result);
        return result;
    }
}
