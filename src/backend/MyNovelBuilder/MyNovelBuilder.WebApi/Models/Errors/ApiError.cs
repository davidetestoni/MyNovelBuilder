using System.Text.Json.Serialization;
using MyNovelBuilder.WebApi.Exceptions;

namespace MyNovelBuilder.WebApi.Models.Errors;

/// <summary>
/// Generic error from the API.
/// </summary>
public class ApiError
{
    /// <summary>
    /// The error code.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// The error message.
    /// </summary>
    public string Message { get; set; }
    
    /// <summary></summary>
    [JsonConstructor]
    public ApiError(string code, string message)
    {
        Code = code;
        Message = message;
    }

    /// <summary>
    /// Creates the <see cref="ApiError"/> from an <see cref="ApiException"/>.
    /// </summary>
    public ApiError(ApiException ex)
    {
        Code = ex.Code;
        Message = ex.Message;
    }
}
