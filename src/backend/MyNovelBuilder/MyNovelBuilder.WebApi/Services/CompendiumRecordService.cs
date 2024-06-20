using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Exceptions;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for compendium records.
/// </summary>
public class CompendiumRecordService : ICompendiumRecordService
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary></summary>
    public CompendiumRecordService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    
    /// <inheritdoc />
    public async Task<CompendiumRecord> GetByIdAsync(Guid id)
    {
        var compendiumRecord = await _unitOfWork.CompendiumRecords.GetWithCompendiumByIdAsync(id);

        if (compendiumRecord is null)
        {
            throw new ApiException(ErrorCodes.CompendiumRecordNotFound, $"Compendium record with ID {id} was not found.");
        }
        
        return compendiumRecord;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<CompendiumRecord>> GetAllAsync()
    {
        return await _unitOfWork.CompendiumRecords.GetAllAsync();
    }

    /// <inheritdoc />
    public async Task CreateAsync(CompendiumRecord compendiumRecord)
    {
        await _unitOfWork.CompendiumRecords.AddAsync(compendiumRecord);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(CompendiumRecord compendiumRecord)
    {
        _unitOfWork.CompendiumRecords.Update(compendiumRecord);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id)
    {
        var compendiumRecord = await GetByIdAsync(id);
        
        _unitOfWork.CompendiumRecords.Remove(compendiumRecord);
        await _unitOfWork.SaveChangesAsync();
    }
}
