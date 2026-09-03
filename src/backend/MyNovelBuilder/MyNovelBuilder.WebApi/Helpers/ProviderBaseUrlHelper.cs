using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;

namespace MyNovelBuilder.WebApi.Helpers;

/// <summary>
/// Helper methods for provider base URLs configured through integrations settings.
/// </summary>
public static class ProviderBaseUrlHelper
{
    /// <summary>
    /// Normalizes a configured HTTP base URL and ensures it can be used for relative requests.
    /// </summary>
    public static Uri NormalizeHttpBaseUri(string? configuredBaseUrl, string defaultBaseUrl, string providerName)
    {
        var rawValue = string.IsNullOrWhiteSpace(configuredBaseUrl)
            ? defaultBaseUrl
            : configuredBaseUrl.Trim();

        if (!Uri.TryCreate(rawValue, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ApiException(
                ErrorCodes.MissingOrInvalidServiceCredentials,
                $"The configured base URL for {providerName} is invalid.");
        }

        return EnsureTrailingSlash(uri);
    }

    /// <summary>
    /// Creates an absolute request URI relative to the configured provider base URL.
    /// </summary>
    public static Uri CreateRequestUri(Uri baseUri, string relativePath) =>
        new(baseUri, relativePath.TrimStart('/'));

    /// <summary>
    /// Creates an absolute WebSocket URI from a configured HTTP base URL.
    /// </summary>
    public static Uri CreateWebSocketUri(Uri baseUri, string relativePath)
    {
        var requestUri = CreateRequestUri(baseUri, relativePath);
        var builder = new UriBuilder(requestUri)
        {
            Scheme = requestUri.Scheme == Uri.UriSchemeHttps ? "wss" : "ws"
        };

        return builder.Uri;
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        if (uri.AbsoluteUri.EndsWith('/'))
        {
            return uri;
        }

        return new Uri($"{uri.AbsoluteUri}/", UriKind.Absolute);
    }
}
