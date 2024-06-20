namespace MyNovelBuilder.WebApi.Exceptions;

/// <summary>
/// Error codes for the API.
/// </summary>
public static class ErrorCodes
{
    /// <summary>
    /// The user is not authorized to perform the requested action.
    /// </summary>
    public const string Unauthorized = "unauthorized";
    
    /// <summary>
    /// The user has not enough permissions to perform the requested action.
    /// </summary>
    public const string Forbidden = "forbidden";
    
    /// <summary>
    /// The request has invalid data.
    /// </summary>
    public const string BadRequest = "bad_request";
    
    /// <summary>
    /// Unmanaged error.
    /// </summary>
    public const string InternalServerError = "internal_server_error";
}