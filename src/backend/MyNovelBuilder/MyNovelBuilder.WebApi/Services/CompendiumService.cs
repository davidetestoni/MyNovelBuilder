using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Data.Repositories;
using MyNovelBuilder.WebApi.Exceptions;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for compendiums.
/// </summary>
public class CompendiumService : ICompendiumService
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary></summary>
    public CompendiumService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    
    /// <inheritdoc />
    public async Task<Compendium> GetByIdAsync(Guid id)
    {
        var compendium = await _unitOfWork.Compendiums.GetWithRecordsByIdAsync(id);

        if (compendium is null)
        {
            throw new ApiException(ErrorCodes.CompendiumNotFound, $"Compendium with ID {id} was not found.");
        }
        
        return compendium;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Compendium>> GetAllAsync()
    {
        return await _unitOfWork.Compendiums.GetAllAsync();
    }

    /// <inheritdoc />
    public async Task CreateAsync(Compendium compendium)
    {
        await _unitOfWork.Compendiums.AddAsync(compendium);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Compendium compendium)
    {
        _unitOfWork.Compendiums.Update(compendium);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id)
    {
        var compendium = await GetByIdAsync(id);
        
        _unitOfWork.Compendiums.Remove(compendium);
        await _unitOfWork.SaveChangesAsync();
    }
}
