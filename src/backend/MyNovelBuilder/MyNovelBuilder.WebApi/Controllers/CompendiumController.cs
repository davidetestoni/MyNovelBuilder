﻿using Mapster;
using Microsoft.AspNetCore.Mvc;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Compendium;
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

    /// <summary></summary>
    public CompendiumController(ICompendiumService compendiumService)
    {
        _compendiumService = compendiumService;
    }
    
    /// <summary>
    /// Get a compendium by its ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<CompendiumDto> GetCompendiumById(Guid id)
    {
        var compendium = await _compendiumService.GetByIdAsync(id);
        var dto = compendium.Adapt<CompendiumDto>();
        
        return dto;
    }
    
    /// <summary>
    /// Get all compendia.
    /// </summary>
    [HttpGet("/api/compendia")]
    public async Task<IEnumerable<CompendiumDto>> GetAllCompendia()
    {
        var compendia = await _compendiumService.GetAllAsync();
        return compendia.Adapt<IEnumerable<CompendiumDto>>();
    }
    
    /// <summary>
    /// Create a compendium.
    /// </summary>
    [HttpPost]
    public async Task<CompendiumDto> CreateCompendium(CreateCompendiumDto createCompendiumDto)
    {
        var compendium = createCompendiumDto.Adapt<Compendium>();
        await _compendiumService.CreateAsync(compendium);
        
        return compendium.Adapt<CompendiumDto>();
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
        
        return compendium.Adapt<CompendiumDto>();
    }
    
    /// <summary>
    /// Delete a compendium by its ID.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task DeleteCompendium(Guid id)
    {
        await _compendiumService.DeleteAsync(id);
    }
}
