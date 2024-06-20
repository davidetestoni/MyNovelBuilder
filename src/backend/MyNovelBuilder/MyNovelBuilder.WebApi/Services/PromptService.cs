using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Exceptions;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for prompts.
/// </summary>
public class PromptService : IPromptService
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary></summary>
    public PromptService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    
    /// <inheritdoc />
    public async Task<Prompt> GetByIdAsync(Guid id)
    {
        var prompt = await _unitOfWork.Prompts.GetByIdAsync(id);
        
        if (prompt is null)
        {
            throw new ApiException(ErrorCodes.PromptNotFound, $"Prompt with ID {id} was not found.");
        }
        
        return prompt;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Prompt>> GetAllAsync()
    {
        return await _unitOfWork.Prompts.GetAllAsync();
    }

    /// <inheritdoc />
    public async Task CreateAsync(Prompt prompt)
    {
        await _unitOfWork.Prompts.AddAsync(prompt);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Prompt prompt)
    {
        _unitOfWork.Prompts.Update(prompt);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id)
    {
        var prompt = await GetByIdAsync(id);
        
        _unitOfWork.Prompts.Remove(prompt);
        await _unitOfWork.SaveChangesAsync();
    }
}
