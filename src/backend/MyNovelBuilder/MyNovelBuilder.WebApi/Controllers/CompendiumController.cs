using Mapster;
using Microsoft.AspNetCore.Mvc;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Compendium;
using MyNovelBuilder.WebApi.Dtos.CompendiumRecord;
using MyNovelBuilder.WebApi.Services;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>
/// Controller for compendia.
/// </summary>
[Route("api/compendium")]
[ApiController]
public class CompendiumController : ControllerBase
{
    private readonly ICompendiumService _compendiumService;
    private readonly ICompendiumRecordService _compendiumRecordService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary></summary>
    public CompendiumController(ICompendiumService compendiumService,
        ICompendiumRecordService compendiumRecordService,
        IHttpContextAccessor httpContextAccessor)
    {
        _compendiumService = compendiumService;
        _compendiumRecordService = compendiumRecordService;
        _httpContextAccessor = httpContextAccessor;
    }
    
    /// <summary>
    /// Get a compendium by its ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<CompendiumDto> GetCompendiumById(Guid id)
    {
        var compendium = await _compendiumService.GetByIdAsync(id);
        var dto = compendium.Adapt<CompendiumDto>();
        await AddRecordsAsync(dto);
        
        return dto;
    }
    
    /// <summary>
    /// Get all compendia.
    /// </summary>
    [HttpGet("/api/compendia")]
    public async Task<IEnumerable<CompendiumDto>> GetAllCompendia()
    {
        var compendia = await _compendiumService.GetAllAsync();
        var dtos = compendia.Adapt<IEnumerable<CompendiumDto>>().ToList();
        var tasks = dtos.Select(AddRecordsAsync);
        await Task.WhenAll(tasks);
        
        return dtos;
    }
    
    /// <summary>
    /// Create a compendium.
    /// </summary>
    [HttpPost]
    public async Task<CompendiumDto> CreateCompendium(CreateCompendiumDto createCompendiumDto)
    {
        var compendium = createCompendiumDto.Adapt<Compendium>();
        await _compendiumService.CreateAsync(compendium);
        
        var dto = compendium.Adapt<CompendiumDto>();
        await AddRecordsAsync(dto);
        
        return dto;
    }
    
    /// <summary>
    /// Update a compendium.
    /// </summary>
    [HttpPut]
    public async Task<CompendiumDto> UpdateCompendium(UpdateCompendiumDto compendiumDto)
    {
        var compendium = await _compendiumService.GetByIdAsync(compendiumDto.Id);
        compendiumDto.Adapt(compendium);
        await _compendiumService.UpdateAsync(compendium);
        
        var dto = compendium.Adapt<CompendiumDto>();
        await AddRecordsAsync(dto);
        
        return dto;
    }
    
    /// <summary>
    /// Delete a compendium by its ID.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task DeleteCompendium(Guid id)
    {
        await _compendiumService.DeleteAsync(id);
    }
    
    private async Task AddRecordsAsync(CompendiumDto compendiumDto)
    {
        var recordDtos = new List<CompendiumRecordOverviewDto>();
        var records = await _compendiumRecordService.GetByCompendiumIdAsync(compendiumDto.Id);
        
        var request = _httpContextAccessor.HttpContext!.Request;
        var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
        
        foreach (var record in records)
        {   
            var recordDto = record.Adapt<CompendiumRecordOverviewDto>();

            if (record.CurrentImageId is not null)
            {
                var urlPath = Path.Combine("static", "compendium", record.Compendium.Id.ToString(),
                    "records", record.Id.ToString(), "gallery", $"{record.CurrentImageId}.png");
                
                recordDto.ImageUrl = $"{baseUrl}/{urlPath.Replace(Path.DirectorySeparatorChar, '/')}";
            }
            
            recordDtos.Add(recordDto);
        }
        
        recordDtos.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        
        compendiumDto.Records = recordDtos;
    }
}
