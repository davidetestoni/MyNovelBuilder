namespace MyNovelBuilder.WebApi.Exceptions;

/// <summary>
/// Exception thrown when an error occurs in the API.
/// </summary>
[Serializable]
public class ApiException : Exception
{
    /// <summary>
    /// The error code.
    /// </summary>
    public string Code { get; set; }
    
    /// <summary>
    /// Exception thrown when an error occurs in the API.
    /// </summary>
    
    public ApiException(string code, string message) : base(message)
    {
        Code = code;
    }
}
