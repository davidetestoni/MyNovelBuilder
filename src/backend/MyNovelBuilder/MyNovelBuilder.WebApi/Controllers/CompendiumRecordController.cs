using Mapster;
using Microsoft.AspNetCore.Mvc;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.CompendiumRecord;
using MyNovelBuilder.WebApi.Services;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>
/// Controller for compendium records.
/// </summary>
[Route("api/compendium-record")]
[ApiController]
public class CompendiumRecordController
{
    private readonly ICompendiumRecordService _compendiumRecordService;
    private readonly ICompendiumService _compendiumService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary></summary>
    public CompendiumRecordController(ICompendiumRecordService compendiumRecordService,
        ICompendiumService compendiumService,
        IHttpContextAccessor httpContextAccessor)
    {
        _compendiumRecordService = compendiumRecordService;
        _compendiumService = compendiumService;
        _httpContextAccessor = httpContextAccessor;
    }
    
    /// <summary>
    /// Get a compendium record by its ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<CompendiumRecordDto> GetCompendiumRecordById(Guid id)
    {
        var record = await _compendiumRecordService.GetByIdAsync(id);
        var dto = record.Adapt<CompendiumRecordDto>();
        await AddImagesAsync(dto);
        
        return dto;
    }
    
    /// <summary>
    /// Get all records for a compendium.
    /// </summary>
    [HttpGet("/api/compendium-records")]
    public async Task<IEnumerable<CompendiumRecordDto>> GetCompendiumRecords(Guid compendiumId)
    {
        var records = await _compendiumRecordService.GetByCompendiumIdAsync(compendiumId);
        var dtos = records.Adapt<IEnumerable<CompendiumRecordDto>>().ToList();
        var tasks = dtos.Select(AddImagesAsync);
        await Task.WhenAll(tasks);
        
        return dtos;
    }
    
    /// <summary>
    /// Create a compendium record.
    /// </summary>
    [HttpPost]
    public async Task<CompendiumRecordDto> CreateCompendiumRecord(CreateCompendiumRecordDto createCompendiumRecordDto)
    {
        var record = createCompendiumRecordDto.Adapt<CompendiumRecord>();
        record.Compendium = await _compendiumService.GetByIdAsync(createCompendiumRecordDto.CompendiumId);
        await _compendiumRecordService.CreateAsync(record);
        
        var dto = record.Adapt<CompendiumRecordDto>();
        await AddImagesAsync(dto);
        
        return dto;
    }
    
    /// <summary>
    /// Update a compendium record.
    /// </summary>
    [HttpPut]
    public async Task<CompendiumRecordDto> UpdateCompendiumRecord(UpdateCompendiumRecordDto compendiumRecordDto)
    {
        var record = await _compendiumRecordService.GetByIdAsync(compendiumRecordDto.Id);
        
        compendiumRecordDto.Adapt(record);
        await _compendiumRecordService.UpdateAsync(record);
        
        var dto = record.Adapt<CompendiumRecordDto>();
        await AddImagesAsync(dto);
        
        return dto;
    }
    
    /// <summary>
    /// Delete a compendium record by its ID.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task DeleteCompendiumRecord(Guid id)
    {
        await _compendiumRecordService.DeleteAsync(id);
    }
    
    /// <summary>
    /// Upload a new image for a compendium record.
    /// </summary>
    [HttpPost("{id:guid}/image")]
    public async Task UploadImage(Guid id, IFormFile file, [FromForm] bool isCurrent = false)
    {
        await _compendiumRecordService.UploadImageAsync(id, file, isCurrent);
    }
    
    /// <summary>
    /// Delete an image from a compendium record.
    /// </summary>
    [HttpDelete("{id:guid}/image/{imageId:guid}")]
    public async Task DeleteImage(Guid id, Guid imageId)
    {
        await _compendiumRecordService.DeleteImageAsync(id, imageId);
    }
    
    /// <summary>
    /// Set an image as the current image for a compendium record.
    /// </summary>
    [HttpPost("{id:guid}/image/{imageId:guid}/set-current")]
    public async Task SetCurrentImage(Guid id, Guid imageId)
    {
        await _compendiumRecordService.SetCurrentImageAsync(id, imageId);
    }

    private async Task AddImagesAsync(CompendiumRecordDto dto)
    {
        var images = await _compendiumRecordService.GetGalleryImagesAsync(dto.Id);

        var request = _httpContextAccessor.HttpContext!.Request;
        var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
        
        dto.Images = images.Select(i => new CompendiumRecordImageDto
        {
            Id = i.Id,
            Url = $"{baseUrl}/{i.Location.Replace(Path.DirectorySeparatorChar, '/')}",
            IsCurrent = dto.CurrentImageId == i.Id
        });
    }
}
