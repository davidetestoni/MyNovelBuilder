using Microsoft.Extensions.Configuration;

namespace MyNovelBuilder.WebApi.Storage;

internal interface IAppDataPathResolver
{
    string Resolve(IReadOnlyList<string> arguments, IConfiguration configuration);
}
