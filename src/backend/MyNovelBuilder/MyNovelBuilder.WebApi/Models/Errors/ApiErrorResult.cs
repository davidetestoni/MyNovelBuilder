using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using MyNovelBuilder.WebApi.Exceptions;

namespace MyNovelBuilder.WebApi.Models.Errors;

/// <summary>
/// Generic error from the API.
/// </summary>
public class ApiErrorResult : IActionResult
{
    /// <inheritdoc />
    public async Task ExecuteResultAsync(ActionContext context)
    {
        var sb = new StringBuilder();
        
        if (!context.ModelState.IsValid)
        {
            foreach (var error in context.ModelState.SelectMany(modelState => 
                         modelState.Value?.Errors ?? new ModelErrorCollection()))
            {
                sb.AppendLine(error.ErrorMessage);
            }
        }
        
        //logic here and then followed by this:
        var apiError = new ApiError(ErrorCodes.BadRequest,
            $"Errors: {sb}");

        var objectResult = new ObjectResult(apiError) { StatusCode = (int)HttpStatusCode.BadRequest };
        await objectResult.ExecuteResultAsync(context);
    }
}
