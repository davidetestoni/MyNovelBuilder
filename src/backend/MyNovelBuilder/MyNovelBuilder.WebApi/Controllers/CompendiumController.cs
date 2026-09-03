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
    private readonly INovelService _novelService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary></summary>
    public CompendiumController(ICompendiumService compendiumService,
        ICompendiumRecordService compendiumRecordService,
        INovelService novelService,
        IHttpContextAccessor httpContextAccessor)
    {
        _compendiumService = compendiumService;
        _compendiumRecordService = compendiumRecordService;
        _novelService = novelService;
        _httpContextAccessor = httpContextAccessor;
    }
    
    /// <summary>
    /// Get a compendium by its ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<CompendiumDto> GetCompendiumById(Guid id, CancellationToken cancellationToken = default)
    {
        var compendium = await _compendiumService.GetByIdAsync(id, cancellationToken);
        var dto = compendium.Adapt<CompendiumDto>();
        await AddRecordsAsync(dto, cancellationToken);
        
        return dto;
    }
    
    /// <summary>
    /// Get all compendia.
    /// </summary>
    [HttpGet("/api/compendia")]
    public async Task<IEnumerable<CompendiumDto>> GetAllCompendia(CancellationToken cancellationToken = default)
    {
        var compendia = await _compendiumService.GetAllAsync(cancellationToken);
        return await MapWithRecordsAsync(compendia, cancellationToken);
    }

    /// <summary>
    /// Get the compendia used by a novel.
    /// </summary>
    [HttpGet("/api/novel/{novelId:guid}/compendia")]
    public async Task<IEnumerable<CompendiumDto>> GetNovelCompendia(
        Guid novelId,
        CancellationToken cancellationToken = default)
    {
        var novel = await _novelService.GetByIdAsync(novelId, cancellationToken);
        return await MapWithRecordsAsync(novel.Compendia, cancellationToken);
    }
    
    /// <summary>
    /// Create a compendium.
    /// </summary>
    [HttpPost]
    public async Task<CompendiumDto> CreateCompendium(
        CreateCompendiumDto createCompendiumDto,
        CancellationToken cancellationToken = default)
    {
        var compendium = createCompendiumDto.Adapt<Compendium>();
        await _compendiumService.CreateAsync(compendium, cancellationToken);
        
        var dto = compendium.Adapt<CompendiumDto>();
        await AddRecordsAsync(dto, cancellationToken);
        
        return dto;
    }
    
    /// <summary>
    /// Update a compendium.
    /// </summary>
    [HttpPut]
    public async Task<CompendiumDto> UpdateCompendium(
        UpdateCompendiumDto compendiumDto,
        CancellationToken cancellationToken = default)
    {
        var compendium = await _compendiumService.GetByIdAsync(compendiumDto.Id, cancellationToken);
        compendiumDto.Adapt(compendium);
        await _compendiumService.UpdateAsync(compendium, cancellationToken);
        
        var dto = compendium.Adapt<CompendiumDto>();
        await AddRecordsAsync(dto, cancellationToken);
        
        return dto;
    }
    
    /// <summary>
    /// Delete a compendium by its ID.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task DeleteCompendium(Guid id, CancellationToken cancellationToken = default)
    {
        // Delete all records associated with the compendium.
        var records = await _compendiumRecordService.GetByCompendiumIdAsync(id, cancellationToken);
        
        foreach (var record in records)
        {
            await _compendiumRecordService.DeleteAsync(record.Id, cancellationToken);
        }
        
        await _compendiumService.DeleteAsync(id, cancellationToken);
    }
    
    private async Task AddRecordsAsync(
        CompendiumDto compendiumDto,
        CancellationToken cancellationToken = default)
    {
        var recordDtos = new List<CompendiumRecordOverviewDto>();
        var records = await _compendiumRecordService.GetByCompendiumIdAsync(
            compendiumDto.Id,
            cancellationToken);
        
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

    private async Task<IEnumerable<CompendiumDto>> MapWithRecordsAsync(
        IEnumerable<Compendium> compendia,
        CancellationToken cancellationToken)
    {
        var dtos = compendia.Adapt<IEnumerable<CompendiumDto>>().ToList();
        foreach (var dto in dtos)
        {
            await AddRecordsAsync(dto, cancellationToken);
        }

        return dtos;
    }
}
