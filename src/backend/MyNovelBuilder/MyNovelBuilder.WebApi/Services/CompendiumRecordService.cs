using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.Media;
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
    public async Task<CompendiumRecord> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var compendiumRecord = await _unitOfWork.CompendiumRecords.GetWithCompendiumByIdAsync(
            id,
            cancellationToken);

        if (compendiumRecord is null)
        {
            throw new ApiException(ErrorCodes.CompendiumRecordNotFound, $"Compendium record with ID {id} was not found.");
        }
        
        return compendiumRecord;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<CompendiumRecord>> GetByCompendiumIdAsync(
        Guid compendiumId,
        CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.CompendiumRecords.GetByCompendiumIdAsync(compendiumId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<CompendiumRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.CompendiumRecords.GetAllAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task CreateAsync(
        CompendiumRecord compendiumRecord,
        CancellationToken cancellationToken = default)
    {
        _unitOfWork.CompendiumRecords.Add(compendiumRecord);
        compendiumRecord.Compendium.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(
        CompendiumRecord compendiumRecord,
        CancellationToken cancellationToken = default)
    {
        _unitOfWork.CompendiumRecords.Update(compendiumRecord);
        compendiumRecord.Compendium.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var compendiumRecord = await GetByIdAsync(id, cancellationToken);
        
        _unitOfWork.CompendiumRecords.Remove(compendiumRecord);
        compendiumRecord.Compendium.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<MediaRef>> GetGalleryMediaAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var record = await GetByIdAsync(id, cancellationToken);
        var localPath = Path.Combine(Globals.StaticFilesRoot, "compendium", record.Compendium.Id.ToString(), "records", id.ToString(), "gallery");
        
        if (!Directory.Exists(localPath))
        {
            return [];
        }
        
        return Directory.GetFiles(localPath).Select(x => new MediaRef
        {
            Id = Guid.Parse(Path.GetFileNameWithoutExtension(x)),
            Location = Path.Combine("static", "compendium", record.Compendium.Id.ToString(), "records", id.ToString(), "gallery", Path.GetFileName(x)),
            IsVideo = Path.GetExtension(x) != ".png"
        });
    }

    /// <inheritdoc />
    public async Task UploadMediaAsync(
        Guid id,
        IFormFile file,
        bool isCurrent = false,
        CancellationToken cancellationToken = default)
    {
        // If it's a video, it cannot be set as current
        if (file.ContentType.StartsWith("video/") && isCurrent)
        {
            isCurrent = false;
        }
        
        var record = await GetByIdAsync(id, cancellationToken);
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);
        var mediaBytes = memoryStream.ToArray();
        var extension = Path.GetExtension(file.FileName);
        
        // If it's not a PNG file, convert it to PNG using ImageSharp.
        if (file.ContentType.StartsWith("image/") && 
            file.ContentType != "image/png")
        {
            using var image = Image.Load(mediaBytes);
            using var outputStream = new MemoryStream();
            await image.SaveAsPngAsync(outputStream, cancellationToken);
            mediaBytes = outputStream.ToArray();
            extension = ".png";
        }

        if (extension is null or "")
        {
            extension = ".png";
        }
        
        var mediaId = Guid.NewGuid();
        var path = Path.Combine(Globals.StaticFilesRoot, "compendium", record.Compendium.Id.ToString(), "records", id.ToString(), "gallery", $"{mediaId}{extension}");
        
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, mediaBytes, cancellationToken);
        
        record.Compendium.UpdatedAt = DateTime.UtcNow;
        
        if (isCurrent)
        {
            record.CurrentImageId = mediaId;
        }
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetCurrentImageAsync(
        Guid id,
        Guid imageId,
        CancellationToken cancellationToken = default)
    {
        var record = await GetByIdAsync(id, cancellationToken);
        
        // If the image is not a .png file, it cannot be set as
        // the current image.
        var localPath = Path.Combine(
            Globals.StaticFilesRoot, 
            "compendium",
            record.Compendium.Id.ToString(),
            "records",
            id.ToString(),
            "gallery",
            $"{imageId}.png");
        
        if (!File.Exists(localPath))
        {
            throw new ApiException(
                ErrorCodes.InvalidFile,
                $"Image with ID {imageId} was not found.");
        }
        
        record.CurrentImageId = imageId;
        record.Compendium.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteMediaAsync(
        Guid id,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        var record = await GetByIdAsync(id, cancellationToken);
        var folderPath = Path.Combine(
            Globals.StaticFilesRoot, 
            "compendium",
            record.Compendium.Id.ToString(),
            "records",
            id.ToString(),
            "gallery");
        
        // Find a file called {mediaId} with any extension.
        var localPath = Directory.GetFiles(
            folderPath, $"{mediaId}.*").FirstOrDefault();
        
        if (localPath is not null && File.Exists(localPath))
        {
            File.Delete(localPath);
        }
        
        record.Compendium.UpdatedAt = DateTime.UtcNow;
        
        if (record.CurrentImageId == mediaId)
        {
            record.CurrentImageId = null;
        }
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
