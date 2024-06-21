using Mapster;
using Microsoft.AspNetCore.Mvc;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Novel;
using MyNovelBuilder.WebApi.Services;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>
/// Controller for novels.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class NovelController : ControllerBase
{
    private readonly INovelService _novelService;

    /// <summary></summary>
    public NovelController(INovelService novelService)
    {
        _novelService = novelService;
    }
    
    /// <summary>
    /// Get a novel by its ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<NovelDto> GetNovelById(Guid id)
    {
        var novel = await _novelService.GetByIdAsync(id);
        return novel.Adapt<NovelDto>();
    }

    /// <summary>
    /// Get all novels.
    /// </summary>
    [HttpGet("/api/novels")]
    public async Task<IEnumerable<NovelDto>> GetAllNovels()
    {
        var novels = await _novelService.GetAllAsync();
        return novels.Adapt<IEnumerable<NovelDto>>();
    }
    
    /// <summary>
    /// Create a novel.
    /// </summary>
    [HttpPost]
    public async Task<NovelDto> CreateNovel(CreateNovelDto createNovelDto)
    {
        var novel = createNovelDto.Adapt<Novel>();
        await _novelService.CreateAsync(novel);
        
        return novel.Adapt<NovelDto>();
    }
    
    /// <summary>
    /// Update a novel.
    /// </summary>
    [HttpPut]
    public async Task<NovelDto> UpdateNovel(UpdateNovelDto updateNovelDto)
    {
        var novel = updateNovelDto.Adapt<Novel>();
        await _novelService.UpdateAsync(novel);
        
        return novel.Adapt<NovelDto>();
    }
    
    /// <summary>
    /// Delete a novel by its ID.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task DeleteNovel(Guid id)
    {
        await _novelService.DeleteAsync(id);
    }
    
    /// <summary>
    /// Get a novel's cover image by its ID.
    /// </summary>
    [HttpGet("{id:guid}/cover-image")]
    public NovelCoverImageDto GetCoverImage(Guid id)
    {
        var location = _novelService.GetCoverImageLocation(id);
        
        return new NovelCoverImageDto
        {
            Id = id,
            CoverImageLocation = location
        };
    }
    
    /// <summary>
    /// Upload a new cover image for a novel.
    /// </summary>
    [HttpPost("{id:guid}/cover-image")]
    public async Task UploadCoverImage(Guid id, IFormFile file)
    {
        await _novelService.UploadCoverImageAsync(id, file);
    }
}
