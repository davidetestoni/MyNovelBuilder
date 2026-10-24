using System.Collections.Concurrent;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Services.Tts;

/// <summary>One synthesis operation per provider, shared by exports, playback and voice design.</summary>
public sealed class ProviderSynthesisGate
{
    private readonly ConcurrentDictionary<TtsProvider, SemaphoreSlim> gates = new();
    /// <summary>Waits for exclusive synthesis access to a provider. Dispose the returned lease after consuming its audio.</summary>
    public async Task<IDisposable> AcquireAsync(TtsProvider provider, CancellationToken ct)
    {
        var gate = gates.GetOrAdd(provider, _ => new(1, 1));
        await gate.WaitAsync(ct);
        return new Lease(gate);
    }
    private sealed class Lease(SemaphoreSlim gate) : IDisposable
    {
        private int disposed;
        public void Dispose() { if (Interlocked.Exchange(ref disposed, 1) == 0) gate.Release(); }
    }
}
