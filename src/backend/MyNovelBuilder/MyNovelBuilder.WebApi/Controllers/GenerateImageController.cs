using Microsoft.AspNetCore.Mvc;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Services;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>
/// Controller for generating images.
/// </summary>
[Route("api/generate/image")]
[ApiController]
public class GenerateImageController : ControllerBase
{
    private readonly IImageGenerationService _imageGen;

    /// <summary></summary>
    public GenerateImageController(IImageGenerationService imageGen)
    {
        _imageGen = imageGen;
    }
    
    /// <summary>
    /// Generate an image.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult> GenerateImageAsync(ImageGenRequestDto dto)
    {
        var image = await _imageGen.GenerateImageAsync(dto);
        
        return File(image, "image/png", "image.png");
    }
}
