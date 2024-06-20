﻿using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Data.Repositories;
using MyNovelBuilder.WebApi.Exceptions;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for novels.
/// </summary>
public class NovelService : INovelService
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary></summary>
    public NovelService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    
    /// <inheritdoc />
    public async Task<Novel> GetByIdAsync(Guid id)
    {
        var novel = await _unitOfWork.Novels.GetWithReferencesByIdAsync(id);

        if (novel is null)
        {
            throw new ApiException(ErrorCodes.NovelNotFound, $"Novel with ID {id} was not found.");
        }
        
        return novel;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Novel>> GetAllAsync()
    {
        return await _unitOfWork.Novels.GetAllAsync();
    }

    /// <inheritdoc />
    public async Task CreateAsync(Novel novel)
    {
        await _unitOfWork.Novels.AddAsync(novel);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Novel novel)
    {
        _unitOfWork.Novels.Update(novel);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id)
    {
        var novel = await GetByIdAsync(id);
        
        _unitOfWork.Novels.Remove(novel);
        await _unitOfWork.SaveChangesAsync();
    }
}