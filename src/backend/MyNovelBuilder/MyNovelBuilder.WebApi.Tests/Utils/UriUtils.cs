using System.Collections.Specialized;
using MyNovelBuilder.WebApi.Tests.Extensions;

namespace MyNovelBuilder.WebApi.Tests.Utils;

internal static class UriUtils
{
    internal static Uri BuildRelativeUri(string path, NameValueCollection queryString)
        => new($"{path}?{queryString.ToQueryString()}", UriKind.Relative);
}
