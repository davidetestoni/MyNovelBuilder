using Mapster;
using Microsoft.AspNetCore.Mvc;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Novel;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.Novels;
using MyNovelBuilder.WebApi.Services;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>
/// Controller for novels.
/// </summary>
[Route("api/novel")]
[ApiController]
public class NovelController : ControllerBase
{
    private readonly INovelService _novelService;
    private readonly ICompendiumService _compendiumService;
    private readonly ICompendiumRecordService _compendiumRecordService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary></summary>
    public NovelController(INovelService novelService,
        ICompendiumService compendiumService,
        ICompendiumRecordService compendiumRecordService,
        IHttpContextAccessor httpContextAccessor)
    {
        _novelService = novelService;
        _compendiumService = compendiumService;
        _compendiumRecordService = compendiumRecordService;
        _httpContextAccessor = httpContextAccessor;
    }
    
    /// <summary>
    /// Get a novel by its ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<NovelDto> GetNovelById(Guid id, CancellationToken cancellationToken = default)
    {
        var novel = await _novelService.GetByIdAsync(id, cancellationToken);
        var dto = novel.Adapt<NovelDto>();
        AddCoverImageUrl(dto);
        
        return dto;
    }
    
    /// <summary>
    /// Get the prose of a novel by its ID.
    /// </summary>
    [HttpGet("{id:guid}/prose")]
    public async Task<Prose> GetNovelProse(Guid id, CancellationToken cancellationToken = default)
    {
        return await _novelService.GetProseAsync(id, cancellationToken);
    }

    /// <summary>
    /// Get all novels.
    /// </summary>
    [HttpGet("/api/novels")]
    public async Task<IEnumerable<NovelDto>> GetAllNovels(CancellationToken cancellationToken = default)
    {
        var novels = await _novelService.GetAllAsync(cancellationToken);
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
    public async Task<NovelDto> CreateNovel(
        CreateNovelDto createNovelDto,
        CancellationToken cancellationToken = default)
    {
        var novel = createNovelDto.Adapt<Novel>();
        await _novelService.CreateAsync(novel, cancellationToken);
        
        var dto = novel.Adapt<NovelDto>();
        AddCoverImageUrl(dto);
        
        return dto;
    }
    
    /// <summary>
    /// Update a novel.
    /// </summary>
    [HttpPut]
    public async Task<NovelDto> UpdateNovel(
        UpdateNovelDto updateNovelDto,
        CancellationToken cancellationToken = default)
    {
        var novel = await _novelService.GetByIdAsync(updateNovelDto.Id, cancellationToken);
        updateNovelDto.Adapt(novel);
        
        // For each compendium id, asynchronously get the
        // compendium and check if it exists,
        // then add it to the novel's compendia.
        novel.Compendia = await Task.WhenAll(updateNovelDto.CompendiumIds
            .Select(id => _compendiumService.GetByIdAsync(id, cancellationToken)));

        // If the main character ID is not null, get the
        // compendium record and set it as the main character.
        if (updateNovelDto.MainCharacterId is not null)
        {
            novel.MainCharacter = await _compendiumRecordService.GetByIdAsync(
                updateNovelDto.MainCharacterId.Value,
                cancellationToken);
            
            // The main character must be part of a compendium that
            // is in the novel's compendia.
            if (novel.Compendia.All(c => c.Id != novel.MainCharacter.Compendium.Id))
            {
                throw new ApiException(
                    ErrorCodes.BadRequest,
                    "The main character must be part of the novel's compendia.");
            }
        }
        
        await _novelService.UpdateAsync(novel, cancellationToken);
        
        var dto = novel.Adapt<NovelDto>();
        AddCoverImageUrl(dto);
        
        return dto;
    }
    
    /// <summary>
    /// Update the prose of a novel.
    /// </summary>
    [HttpPut("{id:guid}/prose")]
    public async Task UpdateNovelProse(Guid id, Prose prose, CancellationToken cancellationToken = default)
    {
        await _novelService.UpdateProseAsync(id, prose, cancellationToken);
        
        // Also update the novel's updated at time.
        var novel = await _novelService.GetByIdAsync(id, cancellationToken);
        await _novelService.UpdateAsync(novel, cancellationToken);
    }
    
    /// <summary>
    /// Delete a novel by its ID.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task DeleteNovel(Guid id, CancellationToken cancellationToken = default)
    {
        await _novelService.DeleteAsync(id, cancellationToken);
    }

    /// <summary>
    /// Upload a new cover image for a novel.
    /// </summary>
    [HttpPost("{id:guid}/cover-image")]
    public async Task UploadCoverImage(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        await _novelService.UploadCoverImageAsync(id, file, cancellationToken);
    }

    /// <summary>
    /// Upload a new prose image for a novel.
    /// </summary>
    [HttpPost("{id:guid}/prose-image")]
    public async Task<IActionResult> UploadProseImage(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var location = await _novelService.UploadProseImageAsync(id, file, cancellationToken);
        return new JsonResult(location);
    }

    /// <summary>
    /// Delete a prose image for a novel.
    /// </summary>
    [HttpDelete("{id:guid}/prose-image/{fileName}")]
    public async Task DeleteProseImage(
        Guid id,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        await _novelService.DeleteProseImageAsync(id, fileName, cancellationToken);
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
