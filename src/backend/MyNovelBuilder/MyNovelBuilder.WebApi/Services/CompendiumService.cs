using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Exceptions;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for compendia.
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
        var compendium = await _unitOfWork.Compendia.GetWithRecordsByIdAsync(id);

        if (compendium is null)
        {
            throw new ApiException(ErrorCodes.CompendiumNotFound, $"Compendium with ID {id} was not found.");
        }
        
        return compendium;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Compendium>> GetAllAsync()
    {
        return await _unitOfWork.Compendia.GetAllAsync();
    }

    /// <inheritdoc />
    public async Task CreateAsync(Compendium compendium)
    {
        await _unitOfWork.Compendia.AddAsync(compendium);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Compendium compendium)
    {
        _unitOfWork.Compendia.Update(compendium);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id)
    {
        var compendium = await GetByIdAsync(id);
        
        _unitOfWork.Compendia.Remove(compendium);
        await _unitOfWork.SaveChangesAsync();
    }
}
