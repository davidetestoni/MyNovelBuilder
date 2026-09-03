namespace MyNovelBuilder.WebApi.Middleware;

internal sealed class ContentSecurityPolicyMiddleware(RequestDelegate next)
{
    internal const string Policy =
        "default-src 'self'; " +
        "base-uri 'self'; " +
        "object-src 'none'; " +
        "frame-ancestors 'none'; " +
        "frame-src 'none'; " +
        "form-action 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' data: https://fonts.gstatic.com; " +
        "img-src 'self' data: blob:; " +
        "media-src 'self' blob:; " +
        "connect-src 'self'; " +
        "worker-src 'self' blob:; " +
        "manifest-src 'self'";

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            if (context.Response.ContentType?.StartsWith(
                    "text/html",
                    StringComparison.OrdinalIgnoreCase) == true)
            {
                // Angular Material and PrimeNG insert component styles at runtime,
                // so style-src cannot drop unsafe-inline without nonce plumbing.
#pragma warning disable S7039
                context.Response.Headers.ContentSecurityPolicy = Policy;
#pragma warning restore S7039
            }

            return Task.CompletedTask;
        });

        await next(context);
    }
}
