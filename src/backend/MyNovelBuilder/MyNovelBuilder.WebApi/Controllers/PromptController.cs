using Mapster;
using Microsoft.AspNetCore.Mvc;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Prompt;
using MyNovelBuilder.WebApi.Services;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>
/// Controller for prompts.
/// </summary>
[Route("api/prompt")]
[ApiController]
public class PromptController : ControllerBase
{
    private readonly IPromptService _promptService;

    /// <summary></summary>
    public PromptController(IPromptService promptService)
    {
        _promptService = promptService;
    }
    
    /// <summary>
    /// Get a prompt by its ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<PromptDto> GetPromptById(Guid id, CancellationToken cancellationToken = default)
    {
        var prompt = await _promptService.GetByIdAsync(id, cancellationToken);
        return prompt.Adapt<PromptDto>();
    }
    
    /// <summary>
    /// Get all prompts.
    /// </summary>
    [HttpGet("/api/prompts")]
    public async Task<IEnumerable<PromptDto>> GetAllPrompts(CancellationToken cancellationToken = default)
    {
        var prompts = await _promptService.GetAllAsync(cancellationToken);
        return prompts.Adapt<IEnumerable<PromptDto>>();
    }
    
    /// <summary>
    /// Create a prompt.
    /// </summary>
    [HttpPost]
    public async Task<PromptDto> CreatePrompt(
        CreatePromptDto createPromptDto,
        CancellationToken cancellationToken = default)
    {
        var prompt = createPromptDto.Adapt<Prompt>();
        await _promptService.CreateAsync(prompt, cancellationToken);
        return prompt.Adapt<PromptDto>();
    }
    
    /// <summary>
    /// Update a prompt.
    /// </summary>
    [HttpPut]
    public async Task<PromptDto> UpdatePrompt(
        UpdatePromptDto updatePromptDto,
        CancellationToken cancellationToken = default)
    {
        var prompt = updatePromptDto.Adapt<Prompt>();
        await _promptService.UpdateAsync(prompt, cancellationToken);
        return prompt.Adapt<PromptDto>();
    }
    
    /// <summary>
    /// Delete a prompt by its ID.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task DeletePrompt(Guid id, CancellationToken cancellationToken = default)
    {
        await _promptService.DeleteAsync(id, cancellationToken);
    }
}
