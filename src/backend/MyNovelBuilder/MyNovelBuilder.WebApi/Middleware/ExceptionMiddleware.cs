using System.Net;
using System.Text.Json;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.Errors;

namespace MyNovelBuilder.WebApi.Middleware;

/// <summary>
/// Middleware that handles exceptions and maps them to HTTP responses.
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// This middleware catches exceptions, logs them and returns a 500 Internal Server Error response.
    /// </summary>
    public ExceptionMiddleware(RequestDelegate next,
        ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invoke the middleware and call the next handler in the chain of
    /// responsibility.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ApiException ex)
        {
            var error = new ApiError(ex);
            await HandleException(error, context, HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Message}", ex.Message);
            var error = new ApiError(ErrorCodes.InternalServerError,
                Globals.Testing ? ex.ToString() : "An error occurred");
            
            await HandleException(error, context, HttpStatusCode.InternalServerError);
        }
    }

    private async Task HandleException(ApiError error,
        HttpContext context, HttpStatusCode statusCode)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var json = JsonSerializer.Serialize(error, _jsonOptions);

        await context.Response.WriteAsync(json);
    }
}
