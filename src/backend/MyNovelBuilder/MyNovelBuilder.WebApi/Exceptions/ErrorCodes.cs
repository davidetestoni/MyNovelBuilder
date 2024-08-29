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
    
    /// <summary>
    /// The novel was not found.
    /// </summary>
    public const string NovelNotFound = "novel_not_found";
    
    /// <summary>
    /// The compendium was not found.
    /// </summary>
    public const string CompendiumNotFound = "compendium_not_found";
    
    /// <summary>
    /// The compendium record was not found.
    /// </summary>
    public const string CompendiumRecordNotFound = "compendium_record_not_found";
    
    /// <summary>
    /// The prompt was not found.
    /// </summary>
    public const string PromptNotFound = "prompt_not_found";
    
    /// <summary>
    /// The cover image is invalid.
    /// </summary>
    public const string InvalidCoverImage = "invalid_cover_image";
    
    /// <summary>
    /// The service credentials are missing or invalid.
    /// </summary>
    public const string MissingOrInvalidServiceCredentials = "missing_or_invalid_service_credentials";
    
    /// <summary>
    /// The external service returned an error.
    /// </summary>
    public const string ExternalServiceError = "external_service_error";
    
    /// <summary>
    /// The prompt context is invalid.
    /// </summary>
    public const string InvalidPromptContext = "invalid_prompt_context";
}
