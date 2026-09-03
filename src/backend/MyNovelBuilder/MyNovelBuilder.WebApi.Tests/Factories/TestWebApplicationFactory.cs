using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MyNovelBuilder.WebApi.Tests.Factories;

public class TestWebApplicationFactory<TProgram>
    : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("test");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["DataFolder"] = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
            }!);
        });
        builder.ConfigureServices(services =>
        {
            // TODO: Override services as needed for testing
        });
    }
}
