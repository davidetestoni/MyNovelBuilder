using System.Net;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>Retries recognizable transient failures, including HTTP rate limits, with a bounded backoff.</summary>
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
    /// <param name="onRetry">Reports the failed attempt, delay, and exception before each retry.</param>
    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        Action<int, TimeSpan, Exception>? onRetry = null)
    {
        var previous = retryScope.Value;
        retryScope.Value = true;
        try { return await ExecuteCoreAsync(action, cancellationToken, delay, onRetry); }
        finally { retryScope.Value = previous; }
    }
    private static async Task<T> ExecuteCoreAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken,
        Func<TimeSpan, CancellationToken, Task>? delay, Action<int, TimeSpan, Exception>? onRetry)
    {
        delay ??= Task.Delay;
        var attempt = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return await action(); }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && attempt < 2)
            {
                var wait = TimeSpan.FromSeconds(1 << attempt);
                onRetry?.Invoke(attempt + 1, wait, ex);
                await delay(wait, cancellationToken);
                attempt++;
            }
            catch (HttpRequestException ex) when (attempt < 2 && IsTransient(ex))
            {
                var wait = ex is RetryableProviderException hinted && hinted.RetryAfter is { } hint
                    ? hint : TimeSpan.FromSeconds(1 << attempt);
                // A long provider cooldown should require a later explicit retry, never retry sooner than its hint.
                if (wait > TimeSpan.FromSeconds(30)) throw;
                wait = wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
                onRetry?.Invoke(attempt + 1, wait, ex);
                await delay(wait, cancellationToken);
                attempt++;
            }
        }
    }
    private static bool IsTransient(HttpRequestException ex) => ex.StatusCode is null
        or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or HttpStatusCode.InternalServerError
        or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;
}

/// <summary>An HTTP failure carrying provider diagnostics and a retry delay.</summary>
/// <param name="status">Provider HTTP status.</param>
/// <param name="retryAfter">Provider-requested delay, or null for the default backoff.</param>
/// <param name="message">Bounded provider error details for server diagnostics.</param>
public sealed class RetryableProviderException(HttpStatusCode status, TimeSpan? retryAfter, string? message = null)
    : HttpRequestException(message ?? "Transient provider request failure.", null, status)
{
    /// <summary>Provider-requested delay before another attempt, when supplied.</summary>
    public TimeSpan? RetryAfter { get; } = retryAfter;
}

/// <summary>Preserves HTTP status, retry hints, and bounded error responses during audiobook retries.</summary>
public sealed class ProviderRetryHintHandler : DelegatingHandler
{
    /// <summary>Preserves provider HTTP failures before adapters can discard their details or status.</summary>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (!ProviderRetry.IsActive || response.IsSuccessStatusCode) return response;
        using (response)
        {
            var header = response.Headers.RetryAfter;
            var delay = header?.Delta ?? (header?.Date - DateTimeOffset.UtcNow);
            string body;
            try
            {
                // Bound diagnostic reads; never copy the outgoing body, auth headers or URL query.
                using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cancellationToken));
                var buffer = new char[8193];
                var count = await reader.ReadBlockAsync(buffer.AsMemory(), cancellationToken);
                body = new string(buffer, 0, Math.Min(count, 8192)) + (count > 8192 ? " [truncated]" : "");
            }
            catch (IOException ex)
            {
                body = $"[Could not read provider error response: {ex.Message}]";
            }
            var requestId = response.Headers.TryGetValues("X-Request-Id", out var ids)
                ? string.Join(",", ids) : "unavailable";
            throw new RetryableProviderException(response.StatusCode, delay,
                $"Provider HTTP {(int)response.StatusCode} ({response.StatusCode}); "
                + $"host: {request.RequestUri?.Host}; request ID: {requestId}; response: {body}");
        }
    }
}
