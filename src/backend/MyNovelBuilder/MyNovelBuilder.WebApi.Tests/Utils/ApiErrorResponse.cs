using MyNovelBuilder.WebApi.Models.Errors;

namespace MyNovelBuilder.WebApi.Tests.Utils;

public class ApiErrorResponse
{
    public ApiError? Info { get; set; }
    public required HttpResponseMessage Response { get; set; }
}
