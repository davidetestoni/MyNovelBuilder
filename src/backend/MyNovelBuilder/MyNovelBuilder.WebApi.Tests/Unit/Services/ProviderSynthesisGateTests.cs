using System.Net;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Services.Tts;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public sealed class ProviderSynthesisGateTests
{
    public static IEnumerable<object[]> AllProviders => Enum.GetValues<TtsProvider>().Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(AllProviders))]
    public async Task EveryProviderFinishesCancelledActiveWorkBeforeStartingNextRequest(TtsProvider key)
    {
        var gate = new ProviderSynthesisGate();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new Provider { BeforeCall = async ct => { entered.TrySetResult(); await release.Task.WaitAsync(ct); } };
        var firstService = new GatedTtsService(provider, key, gate);
        var secondService = new GatedTtsService(provider, key, gate);
        using var cancellation = new CancellationTokenSource();
        var first = firstService.GenerateAudioAsync(new() { VoiceId = "voice", Message = "first" }, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        var second = secondService.GenerateAudioAsync(new() { VoiceId = "voice", Message = "second" });
        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        Assert.Equal(1, provider.Calls);
        release.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await second.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, provider.Calls);
    }

    [Theory]
    [MemberData(nameof(AllProviders))]
    public async Task EveryProviderDrainsClosedPlaybackBeforeStartingNextRequest(TtsProvider key)
    {
        var gate = new ProviderSynthesisGate();
        var audio = new DrainingStream();
        var provider = new Provider { AudioStream = audio };
        var playback = new GatedTtsService(provider, key, gate);
        var export = new GatedTtsService(provider, key, gate);
        var stream = await playback.GenerateAudioStreamAsync(new() { VoiceId = "voice", Message = "preview" });
        var closing = stream.DisposeAsync().AsTask();
        await audio.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var next = export.GenerateAudioAsync(new() { VoiceId = "voice", Message = "export" });
        Assert.False(closing.IsCompleted);
        Assert.False(next.IsCompleted);
        Assert.Equal(0, provider.Calls);
        audio.Release.SetResult();
        await closing.WaitAsync(TimeSpan.FromSeconds(5));
        await next.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task StreamConsumptionBlocksSameProviderAcrossServiceInstances_ButNotOtherProviders()
    {
        var gate = new ProviderSynthesisGate();
        var provider = new Provider();
        var playback = new GatedTtsService(provider, TtsProvider.ElevenLabs, gate);
        var export = new GatedTtsService(provider, TtsProvider.ElevenLabs, gate);
        var other = new GatedTtsService(provider, TtsProvider.Kokoro, gate);
        var stream = await playback.GenerateAudioStreamAsync(new() { VoiceId = "voice", Message = "playback" });
        var waiting = export.GenerateAudioAsync(new() { VoiceId = "voice", Message = "export" });
        Assert.False(waiting.IsCompleted);
        await other.GenerateAudioAsync(new() { VoiceId = "voice", Message = "other" }).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(waiting.IsCompleted);
        await stream.DisposeAsync();
        await waiting.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GateReleasesOnFailureAndCancellationDoesNotStartWaitingRequest()
    {
        var gate = new ProviderSynthesisGate();
        var provider = new Provider();
        var tts = new GatedTtsService(provider, TtsProvider.ElevenLabs, gate);
        using var stream = await tts.GenerateAudioStreamAsync(new() { VoiceId = "voice", Message = "playback" });
        using var cancellation = new CancellationTokenSource();
        var wait = tts.GenerateAudioAsync(new() { VoiceId = "voice", Message = "waiting" }, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        Assert.Equal(0, provider.Calls);
        stream.Dispose();
        provider.Fail = true;
        await Assert.ThrowsAsync<IOException>(() => tts.GenerateAudioAsync(new() { VoiceId = "voice", Message = "fail" }));
        provider.Fail = false;
        await tts.GenerateAudioAsync(new() { VoiceId = "voice", Message = "retry" }).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, 1)]
    [InlineData(HttpStatusCode.Forbidden, 1)]
    [InlineData(HttpStatusCode.PaymentRequired, 1)]
    [InlineData(HttpStatusCode.BadRequest, 1)]
    [InlineData(HttpStatusCode.TooManyRequests, 1)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 3)]
    [InlineData(HttpStatusCode.BadGateway, 3)]
    public async Task RetryIsBoundedAndDoesNotLoopOnAuthQuotaOrValidation(HttpStatusCode status, int expected)
    {
        var calls = 0;
        var waits = new List<TimeSpan>();
        await Assert.ThrowsAsync<HttpRequestException>(() => ProviderRetry.ExecuteAsync<int>(() =>
        {
            calls++;
            throw new HttpRequestException("private response", null, status);
        }, default, (delay, _) => { waits.Add(delay); return Task.CompletedTask; }));
        Assert.Equal(expected, calls);
        Assert.Equal(expected - 1, waits.Count);
        if (waits.Count == 2) Assert.Equal(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) }, waits);
    }

    [Fact]
    public async Task ExplicitRateLimitHintsAreHonored_LongCooldownRequiresManualRetry()
    {
        var waits = new List<TimeSpan>();
        await Assert.ThrowsAsync<RetryableProviderException>(() => ProviderRetry.ExecuteAsync<int>(
            () => throw new RetryableProviderException(HttpStatusCode.TooManyRequests, TimeSpan.FromSeconds(7)),
            default, (delay, _) => { waits.Add(delay); return Task.CompletedTask; }));
        Assert.Equal(new[] { TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(7) }, waits);
        waits.Clear();
        await Assert.ThrowsAsync<RetryableProviderException>(() => ProviderRetry.ExecuteAsync<int>(
            () => throw new RetryableProviderException(HttpStatusCode.TooManyRequests, TimeSpan.FromMinutes(2)),
            default, (delay, _) => { waits.Add(delay); return Task.CompletedTask; }));
        Assert.Empty(waits);
    }

    [Fact]
    public async Task HttpHandlerPreservesRetryAfterWithoutRetainingResponseBody()
    {
        using var client = new HttpClient(new ProviderRetryHintHandler { InnerHandler = new HintResponse() });
        var error = await Assert.ThrowsAsync<RetryableProviderException>(() => ProviderRetry.ExecuteAsync(() => client.GetAsync("http://localhost/test"), default, (_, _) => Task.CompletedTask));
        Assert.Equal(TimeSpan.FromSeconds(8), error.RetryAfter);
        Assert.DoesNotContain("secret", error.Message);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlaybackCacheFailureStillDisposesStreamAndReleasesProvider(bool asynchronous)
    {
        var gate = new ProviderSynthesisGate();
        var provider = new Provider();
        var service = new GatedTtsService(provider, TtsProvider.ElevenLabs, gate);
        var stream = await service.GenerateAudioStreamAsync(new() { VoiceId = "voice", Message = "playback" });
        var cached = new CachingReadStream(stream, (_, _) => throw new IOException("disk full"), default);
        await cached.CopyToAsync(Stream.Null);
        if (asynchronous) await Assert.ThrowsAsync<IOException>(async () => await cached.DisposeAsync());
        else Assert.Throws<IOException>(() => cached.Dispose());
        await service.GenerateAudioAsync(new() { VoiceId = "voice", Message = "next" }).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RetryHintHandlerLeavesOrdinaryPlaybackResponsesUnchanged()
    {
        using var client = new HttpClient(new ProviderRetryHintHandler { InnerHandler = new HintResponse() });
        using var response = await client.GetAsync("http://localhost/test");
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    private sealed class HintResponse : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("secret") };
            response.Headers.RetryAfter = new(TimeSpan.FromSeconds(8));
            return Task.FromResult(response);
        }
    }
    private sealed class DrainingStream : MemoryStream
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            await base.CopyToAsync(destination, bufferSize, cancellationToken);
        }
    }

    private sealed class Provider : ITtsService
    {
        public bool Fail;
        public int Calls;
        public Func<CancellationToken, Task>? BeforeCall;
        public Stream? AudioStream;
        public AudioFormat OutputAudioFormat => AudioFormat.Wav;
        public async Task<byte[]> GenerateAudioAsync(TtsRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (BeforeCall is not null) await BeforeCall(cancellationToken);
            if (Fail) throw new IOException();
            return new byte[10];
        }
        public Task<Stream> GenerateAudioStreamAsync(TtsRequest request, CancellationToken cancellationToken = default) => Task.FromResult(AudioStream ?? new MemoryStream(new byte[10]));
        public Task<IEnumerable<TtsModelDto>> GetModelsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<decimal?> GetBalanceUsdAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
