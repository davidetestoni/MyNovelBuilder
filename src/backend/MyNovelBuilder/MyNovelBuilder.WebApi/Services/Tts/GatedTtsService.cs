using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.Prompts;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Services.TextGeneration;

namespace MyNovelBuilder.WebApi.Services.Tts;

/// <summary>Serializes synthesis for every provider, retaining its slot until generation and streamed response consumption finish.</summary>
public sealed class GatedTtsService(ITtsService inner, TtsProvider provider, ProviderSynthesisGate gate) : ITtsService
{
    /// <inheritdoc/>
    public AudioFormat OutputAudioFormat => inner.OutputAudioFormat;
    /// <inheritdoc/>
    public bool SupportsTextEmphasis(string? modelId) => inner.SupportsTextEmphasis(modelId);
    /// <inheritdoc/>
    public IReadOnlyList<PromptMessage> GetEmphasisMessages(string text) => inner.GetEmphasisMessages(text);
    /// <inheritdoc/>
    public bool SupportsVoiceDesign() => inner.SupportsVoiceDesign();
    /// <inheritdoc/>
    public Task<string> EmphasizeTextAsync(TtsRequest request, Func<CancellationToken, ValueTask<ITextGenerationService>> textGenerationServiceFactory, CancellationToken cancellationToken = default) => inner.EmphasizeTextAsync(request, textGenerationServiceFactory, cancellationToken);
    /// <inheritdoc/>
    public Task<IEnumerable<TtsModelDto>> GetModelsAsync(CancellationToken cancellationToken = default) => inner.GetModelsAsync(cancellationToken);
    /// <inheritdoc/>
    public Task<decimal?> GetBalanceUsdAsync(CancellationToken cancellationToken = default) => inner.GetBalanceUsdAsync(cancellationToken);
    /// <inheritdoc/>
    /// <remarks>Holds the provider gate until the operation finishes. Cancellation stops queued work; active remote work drains before releasing the slot.</remarks>
    public async Task<byte[]> VoiceDesignAsync(string prompt, WritingLanguage language, string voiceDescription, CancellationToken cancellationToken = default)
    {
        using var lease = await gate.AcquireAsync(provider, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var result = await inner.VoiceDesignAsync(prompt, language, voiceDescription, CancellationToken.None);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }
    /// <inheritdoc/>
    /// <remarks>Holds the provider gate until the operation finishes. Cancellation stops queued work; active remote work drains before releasing the slot.</remarks>
    public async Task<byte[]> GenerateAudioAsync(TtsRequest request, CancellationToken cancellationToken = default)
    {
        using var lease = await gate.AcquireAsync(provider, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        // Disconnecting the caller need not stop remote GPU work. Finish this operation before
        // releasing the provider slot, then let cancellation stop the caller's remaining chunks.
        var result = await inner.GenerateAudioAsync(request, CancellationToken.None);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }
    /// <summary>Acquires the provider gate and holds it through playback or draining when the caller disposes early.</summary>
    public async Task<Stream> GenerateAudioStreamAsync(TtsRequest request, CancellationToken cancellationToken = default)
    {
        var lease = await gate.AcquireAsync(provider, cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stream = new LeasedStream(await inner.GenerateAudioStreamAsync(request, CancellationToken.None), lease);
            if (cancellationToken.IsCancellationRequested)
            {
                await stream.DisposeAsync();
                cancellationToken.ThrowIfCancellationRequested();
            }
            return stream;
        }
        catch { lease.Dispose(); throw; }
    }
    private sealed class LeasedStream(Stream inner, IDisposable lease) : Stream
    {
        private int disposed;
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => inner.Read(buffer);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void Flush() => inner.Flush();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) DisposeAsync().AsTask().GetAwaiter().GetResult();
            base.Dispose(disposing);
        }
        public override async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            try
            {
                // Streaming providers can produce subsequent chunks in the background. Drain
                // them before releasing the slot, even when playback closed its consumer early.
                try { await inner.CopyToAsync(Stream.Null, CancellationToken.None); }
                catch { /* A failed producer is finished; disposal must still release the slot. */ }
            }
            finally
            {
                try { await inner.DisposeAsync(); }
                finally { lease.Dispose(); GC.SuppressFinalize(this); }
            }
        }
    }
}
