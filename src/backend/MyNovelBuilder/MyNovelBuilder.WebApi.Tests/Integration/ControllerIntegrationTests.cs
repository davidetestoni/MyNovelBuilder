using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.Errors;
using MyNovelBuilder.WebApi.Tests.Factories;
using MyNovelBuilder.WebApi.Tests.Utils;
using Xunit.Abstractions;

namespace MyNovelBuilder.WebApi.Tests.Integration;

public class ControllerIntegrationTests  : IClassFixture<TestWebApplicationFactory<Program>>
{
    private readonly ITestOutputHelper _output;
    protected TestWebApplicationFactory<Program> Factory { get; }
    private JsonSerializerOptions JsonOptions { get; } = new();
    protected IUnitOfWork UnitOfWork => Factory.Services.GetRequiredService<IUnitOfWork>();
    
    protected ControllerIntegrationTests(TestWebApplicationFactory<Program> factory,
        ITestOutputHelper output)
    {
        _output = output;
        Factory = factory;
        
        var enumConverter = new JsonStringEnumConverter(JsonNamingPolicy.CamelCase);
        JsonOptions.Converters.Add(enumConverter);
        JsonOptions.Converters.Add(new UtcDateTimeConverter());
        JsonOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "test");
    }
    
    protected async Task ResetDbAsync()
    {   
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.ChangeTracker.Clear();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
    }
    
        private async Task<Result<T, ApiErrorResponse>> SendRequestAsync<T>(HttpClient client,
        HttpMethod method, Uri url, object? dto = null)
    {
        using var request = new HttpRequestMessage();
        request.RequestUri = url;
        request.Method = method;

        if (dto is not null)
        {
            request.Content = JsonContent.Create(dto, MediaTypeHeaderValue.Parse("application/json"), JsonOptions);
        }

        var response = await client.SendAsync(request);
        var jsonResponse = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
        {
            _output.WriteLine($"Response status code: {response.StatusCode}");
            _output.WriteLine($"Response content: {jsonResponse}");
            
            try
            {
                return new ApiErrorResponse
                {
                    Info = JsonSerializer.Deserialize<ApiError>(jsonResponse, JsonOptions)!,
                    Response = response
                }!;
            }
            catch (JsonException)
            {
                return new ApiErrorResponse
                {
                    Response = response
                }!;
            }
        }

        return !string.IsNullOrEmpty(jsonResponse)
            ? JsonSerializer.Deserialize<T>(jsonResponse, JsonOptions)!
            : default(Result<T, ApiErrorResponse>);
    }
    
    // GET
    protected Task<Result<T, ApiErrorResponse>> GetJsonAsync<T>(HttpClient client, string url)
        => GetJsonAsync<T>(client, new Uri(url, UriKind.Relative));
    
    protected Task<Result<T, ApiErrorResponse>> GetJsonAsync<T>(HttpClient client, Uri url)
        => SendRequestAsync<T>(client, HttpMethod.Get, url);
    
    // POST
    protected async Task<ApiErrorResponse?> PostJsonAsync(HttpClient client, string url, object dto)
        => await PostJsonAsync(client, new Uri(url, UriKind.Relative), dto);

    private async Task<ApiErrorResponse?> PostJsonAsync(HttpClient client, Uri url, object dto)
    {
        var response = await SendRequestAsync<object>(client, HttpMethod.Post, url, dto);
        return response.IsOk ? null : response.Error;
    }
    
    protected async Task<Result<T, ApiErrorResponse>> PostJsonAsync<T>(HttpClient client, string url, object dto)
        => await PostJsonAsync<T>(client, new Uri(url, UriKind.Relative), dto);

    private async Task<Result<T, ApiErrorResponse>> PostJsonAsync<T>(HttpClient client, Uri url, object dto)
        => await SendRequestAsync<T>(client, HttpMethod.Post, url, dto);
    
    // PUT
    protected async Task<Result<T, ApiErrorResponse>> PutJsonAsync<T>(HttpClient client, string url, object dto)
        => await PutJsonAsync<T>(client, new Uri(url, UriKind.Relative), dto);

    private async Task<Result<T, ApiErrorResponse>> PutJsonAsync<T>(HttpClient client, Uri url, object dto)
        => await SendRequestAsync<T>(client, HttpMethod.Put, url, dto);
    
    // PATCH
    protected async Task<Result<T, ApiErrorResponse>> PatchJsonAsync<T>(HttpClient client, string url, object dto)
        => await PatchJsonAsync<T>(client, new Uri(url, UriKind.Relative), dto);

    private async Task<Result<T, ApiErrorResponse>> PatchJsonAsync<T>(HttpClient client, Uri url, object dto)
        => await SendRequestAsync<T>(client, HttpMethod.Patch, url, dto);
    
    // DELETE
    protected async Task<ApiErrorResponse?> DeleteAsync(HttpClient client, string url)
        => await DeleteAsync(client, new Uri(url, UriKind.Relative));
    
    private async Task<ApiErrorResponse?> DeleteAsync(HttpClient client, Uri url)
    {
        var response = await SendRequestAsync<object>(client, HttpMethod.Delete, url);
        return response.IsOk ? null : response.Error;
    }
}
