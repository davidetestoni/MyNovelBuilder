using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MyNovelBuilder.WebApi.Tests.Factories;

public class TestWebApplicationFactory<TProgram>
    : WebApplicationFactory<TProgram> where TProgram : class
{
    private readonly string _dataFolder = Path.Combine(
        Path.GetTempPath(),
        Path.GetRandomFileName());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("test");
        builder.UseSetting("DataFolder", _dataFolder);
        builder.ConfigureServices(services =>
        {
            // Tests reset the database/files between requests. Worker lifecycle is covered separately.
            var worker = services.SingleOrDefault(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                && d.ImplementationType == typeof(MyNovelBuilder.WebApi.Services.AudiobookWorker));
            if (worker is not null) services.Remove(worker);
        });
    }
}
