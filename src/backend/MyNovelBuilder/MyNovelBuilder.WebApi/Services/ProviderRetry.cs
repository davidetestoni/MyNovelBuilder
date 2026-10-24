using System.Net;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>Retry only recognizable transient failures. Opaque provider errors/auth/quota are not retried.</summary>
public static class ProviderRetry
{
    private static readonly AsyncLocal<bool> retryScope = new();
    internal static bool IsActive => retryScope.Value;
    /// <summary>Runs an operation with at most three attempts for recognized transient failures.</summary>
    /// <remarks>Uses one- and two-second delays unless the provider supplies a hint; hints over 30 seconds require a later explicit retry.</remarks>
    /// <typeparam name="T">Operation result type.</typeparam>
    /// <param name="action">Operation to invoke again on a retry; it must tolerate repeated calls.</param>
    /// <param name="cancellationToken">Stops execution and retry delays.</param>
    /// <param name="delay">Optional delay implementation for tests; defaults to Task.Delay.</param>
    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        var previous = retryScope.Value;
        retryScope.Value = true;
        try { return await ExecuteCoreAsync(action, cancellationToken, delay); }
        finally { retryScope.Value = previous; }
    }
    private static async Task<T> ExecuteCoreAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken,
        Func<TimeSpan, CancellationToken, Task>? delay)
    {
        delay ??= Task.Delay;
        for (int attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return await action(); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < 2)
            {
                await delay(TimeSpan.FromSeconds(1 << attempt), cancellationToken);
            }
            catch (HttpRequestException ex) when (attempt < 2 && IsTransient(ex))
            {
                var wait = ex is RetryableProviderException hinted && hinted.RetryAfter is { } hint
                    ? hint : TimeSpan.FromSeconds(1 << attempt);
                // A long provider cooldown should require a later explicit retry, never retry sooner than its hint.
                if (wait > TimeSpan.FromSeconds(30)) throw;
                await delay(wait < TimeSpan.Zero ? TimeSpan.Zero : wait, cancellationToken);
            }
        }
    }
    private static bool IsTransient(HttpRequestException ex) => ex.StatusCode is null
        or HttpStatusCode.RequestTimeout or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout
        || ex is RetryableProviderException && ex.StatusCode == HttpStatusCode.TooManyRequests;
}

/// <summary>A transient HTTP failure carrying a retry delay without retaining the provider response body.</summary>
/// <param name="status">Provider HTTP status.</param>
/// <param name="retryAfter">Provider-requested delay, or null for the default backoff.</param>
public sealed class RetryableProviderException(HttpStatusCode status, TimeSpan? retryAfter)
    : HttpRequestException("Transient provider request failure.", null, status)
{
    /// <summary>Provider-requested delay before another attempt, when supplied.</summary>
    public TimeSpan? RetryAfter { get; } = retryAfter;
}

/// <summary>Preserves explicit retry hints without copying potentially sensitive response bodies.</summary>
public sealed class ProviderRetryHintHandler : DelegatingHandler
{
    /// <summary>Converts 503 responses and explicitly hinted 429 responses into retry exceptions only inside an active retry scope.</summary>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (!ProviderRetry.IsActive) return response;
        var header = response.Headers.RetryAfter;
        // A 429 without an explicit retry hint may mean exhausted quota; leave it to the provider adapter.
        if (response.StatusCode is HttpStatusCode.ServiceUnavailable ||
            (response.StatusCode == HttpStatusCode.TooManyRequests && header is not null))
        {
            var delay = header?.Delta ?? (header?.Date - DateTimeOffset.UtcNow);
            var status = response.StatusCode;
            response.Dispose();
            throw new RetryableProviderException(status, delay);
        }
        return response;
    }
}
