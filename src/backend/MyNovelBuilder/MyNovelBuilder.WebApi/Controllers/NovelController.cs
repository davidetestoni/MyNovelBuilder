using Microsoft.AspNetCore.Mvc;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>
/// Controller for novels.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class NovelController : ControllerBase
{
    /// <summary>
    /// Get a novel by its ID.
    /// </summary>
    [HttpGet("{id}")]
    public IActionResult GetNovelById(int id)
    {
        // Your logic to get a novel by id
        // For example: var novel = _novelService.GetById(id);
        // return Ok(novel);
        return Ok($"Returning novel with id {id}");
    }

    /// <summary>
    /// Get all novels.
    /// </summary>
    [HttpGet("/api/novels")]
    public IActionResult GetAllNovels()
    {
        // Your logic to get all novels
        // For example: var novels = _novelService.GetAll();
        // return Ok(novels);
        return Ok("Returning all novels");
    }
}
