﻿using Microsoft.AspNetCore.Mvc;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.Images;
using SixLabors.ImageSharp;

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
    public async Task<IEnumerable<CompendiumRecord>> GetByCompendiumIdAsync(Guid compendiumId)
    {
        return await _unitOfWork.CompendiumRecords.GetByCompendiumIdAsync(compendiumId);
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

    /// <inheritdoc />
    public async Task<IEnumerable<ImageRef>> GetGalleryImagesAsync(Guid id)
    {
        var record = await GetByIdAsync(id);
        var localPath = Path.Combine(Globals.StaticFilesRoot, "compendium", record.Compendium.Id.ToString(), "records", id.ToString(), "gallery");
        
        if (!Directory.Exists(localPath))
        {
            return Array.Empty<ImageRef>();
        }
        
        return Directory.GetFiles(localPath, "*.png").Select(x => new ImageRef
        {
            Id = Guid.Parse(Path.GetFileNameWithoutExtension(x)),
            Location = Path.Combine("static", "compendium", record.Compendium.Id.ToString(), "records", id.ToString(), "gallery", Path.GetFileName(x))
        });
    }

    /// <inheritdoc />
    public async Task UploadImageAsync(Guid id, IFormFile file, bool isCurrent = false)
    {
        var record = await GetByIdAsync(id);
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        var imageBytes = memoryStream.ToArray();
        
        // If it's not a PNG file, convert it to PNG using ImageSharp.
        if (file.ContentType != "image/png")
        {
            using var image = Image.Load(imageBytes);
            using var outputStream = new MemoryStream();
            await image.SaveAsPngAsync(outputStream);
            imageBytes = outputStream.ToArray();
        }
        
        var imageId = Guid.NewGuid();
        var path = Path.Combine(Globals.StaticFilesRoot, "compendium", record.Compendium.Id.ToString(), "records", id.ToString(), "gallery", $"{imageId}.png");
        
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, imageBytes);
        
        if (isCurrent)
        {
            record.CurrentImageId = imageId;
            await UpdateAsync(record);
        }
    }

    /// <inheritdoc />
    public async Task SetCurrentImageAsync(Guid id, Guid imageId)
    {
        var record = await GetByIdAsync(id);
        record.CurrentImageId = imageId;
        
        await UpdateAsync(record);
    }

    /// <inheritdoc />
    public async Task DeleteImageAsync(Guid id, Guid imageId)
    {
        var record = await GetByIdAsync(id);
        var localPath = Path.Combine(Globals.StaticFilesRoot, "compendium", record.Compendium.Id.ToString(), "records", id.ToString(), "gallery", $"{imageId}.png");
        
        if (File.Exists(localPath))
        {
            File.Delete(localPath);
        }
    }
}
