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
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary></summary>
    public NovelController(INovelService novelService,
        IHttpContextAccessor httpContextAccessor)
    {
        _novelService = novelService;
        _httpContextAccessor = httpContextAccessor;
    }
    
    /// <summary>
    /// Get a novel by its ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<NovelDto> GetNovelById(Guid id)
    {
        var novel = await _novelService.GetByIdAsync(id);
        var dto = novel.Adapt<NovelDto>();
        AddCoverImageUrl(dto);
        
        return dto;
    }

    /// <summary>
    /// Get all novels.
    /// </summary>
    [HttpGet("/api/novels")]
    public async Task<IEnumerable<NovelDto>> GetAllNovels()
    {
        var novels = await _novelService.GetAllAsync();
        return novels.Adapt<IEnumerable<NovelDto>>()
            .Select(dto =>
            {
                AddCoverImageUrl(dto);
                return dto;
            });
    }
    
    /// <summary>
    /// Create a novel.
    /// </summary>
    [HttpPost]
    public async Task<NovelDto> CreateNovel(CreateNovelDto createNovelDto)
    {
        var novel = createNovelDto.Adapt<Novel>();
        await _novelService.CreateAsync(novel);
        
        var dto = novel.Adapt<NovelDto>();
        AddCoverImageUrl(dto);
        
        return dto;
    }
    
    /// <summary>
    /// Update a novel.
    /// </summary>
    [HttpPut]
    public async Task<NovelDto> UpdateNovel(UpdateNovelDto updateNovelDto)
    {
        var novel = await _novelService.GetByIdAsync(updateNovelDto.Id);
        updateNovelDto.Adapt(novel);
        await _novelService.UpdateAsync(novel);
        
        var dto = novel.Adapt<NovelDto>();
        AddCoverImageUrl(dto);
        
        return dto;
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
    /// Upload a new cover image for a novel.
    /// </summary>
    [HttpPost("{id:guid}/cover-image")]
    public async Task UploadCoverImage(Guid id, IFormFile file)
    {
        await _novelService.UploadCoverImageAsync(id, file);
    }

    private void AddCoverImageUrl(NovelDto dto)
    {
        var urlPath = _novelService.GetCoverImageLocation(dto.Id);
        
        if (urlPath is null)
        {
            dto.CoverImageUrl = null;
            return;
        }
        
        var request = _httpContextAccessor.HttpContext!.Request;
        var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
        dto.CoverImageUrl = $"{baseUrl}/{urlPath.Replace(Path.DirectorySeparatorChar, '/')}";
    }
}
