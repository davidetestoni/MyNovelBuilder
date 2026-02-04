using System.Collections.Specialized;

namespace MyNovelBuilder.WebApi.Tests.Extensions;

/// <summary>
/// Extension methods for <see cref="NameValueCollection"/>.
/// </summary>
public static class NameValueCollectionExtensions
{
    /// <summary>
    /// Helper method to build query string from NameValueCollection
    /// </summary>
    public static string ToQueryString(this NameValueCollection parameters)
    {
        var queryString = string.Join("&",
            parameters.AllKeys
                .Where(key => key is not null && parameters[key] is not null)
                .Select(key => $"{Uri.EscapeDataString(key!)}={Uri.EscapeDataString(parameters[key]!)}"));

        return queryString;
    }
}
