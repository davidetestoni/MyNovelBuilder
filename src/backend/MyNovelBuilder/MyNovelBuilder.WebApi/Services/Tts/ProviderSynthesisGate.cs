using System.Collections.Concurrent;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Services.Tts;

/// <summary>Shared provider capacity, serial by default with explicit audiobook concurrency overrides.</summary>
public sealed class ProviderSynthesisGate
{
    private readonly ConcurrentDictionary<TtsProvider, Capacity> gates = new();
    private readonly AsyncLocal<Allowance?> allowance = new();
    private sealed record Allowance(TtsProvider Provider, int Limit);

    /// <summary>Allows this audiobook's child operations to share up to the selected number of provider slots.</summary>
    public IDisposable BeginAudiobookScope(TtsProvider provider, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 8);
        var previous = allowance.Value;
        allowance.Value = new(provider, limit);
        return new Lease(() => allowance.Value = previous);
    }

    /// <summary>Waits for provider capacity. Dispose only after generation and response consumption finish.</summary>
    public async Task<IDisposable> AcquireAsync(TtsProvider provider, CancellationToken ct)
    {
        var capacity = gates.GetOrAdd(provider, _ => new());
        var limit = allowance.Value is { } selected && selected.Provider == provider ? selected.Limit : 1;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            Task changed;
            lock (capacity)
            {
                if (capacity.Active < limit)
                {
                    capacity.Active++;
                    return new Lease(() => Release(capacity));
                }
                changed = capacity.Changed.Task;
            }
            await changed.WaitAsync(ct);
        }
    }

    private static void Release(Capacity capacity)
    {
        lock (capacity)
        {
            capacity.Active--;
            var changed = capacity.Changed;
            capacity.Changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            changed.TrySetResult();
        }
    }

    private sealed class Capacity
    {
        public int Active;
        public TaskCompletionSource Changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class Lease(Action release) : IDisposable
    {
        private Action? releaseSlot = release;
        public void Dispose() => Interlocked.Exchange(ref releaseSlot, null)?.Invoke();
    }
}
