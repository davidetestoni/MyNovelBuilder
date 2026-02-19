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
    public async Task<Prompt> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var prompt = await _unitOfWork.Prompts.GetByIdAsync(id, cancellationToken);
        
        if (prompt is null)
        {
            throw new ApiException(ErrorCodes.PromptNotFound, $"Prompt with ID {id} was not found.");
        }
        
        return prompt;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Prompt>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Prompts.GetAllAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task CreateAsync(Prompt prompt, CancellationToken cancellationToken = default)
    {
        _unitOfWork.Prompts.Add(prompt);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Prompt prompt, CancellationToken cancellationToken = default)
    {
        _unitOfWork.Prompts.Update(prompt);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var prompt = await GetByIdAsync(id, cancellationToken);
        
        _unitOfWork.Prompts.Remove(prompt);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
