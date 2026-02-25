using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.Novels;
using MyNovelBuilder.WebApi.Options;
using SixLabors.ImageSharp;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for novels.
/// </summary>
public class NovelService : INovelService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly string _dataFolder;
    private readonly string _staticFilesRoot;

    /// <summary></summary>
    public NovelService(
        IUnitOfWork unitOfWork,
        IOptions<AppStorageOptions> storageOptions)
    {
        _unitOfWork = unitOfWork;
        _jsonSerializerOptions = JsonDefaults.Options;
        _dataFolder = storageOptions.Value.DataFolder;
        _staticFilesRoot = storageOptions.Value.StaticFilesRoot;
    }
    
    /// <inheritdoc />
    public async Task<Novel> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var novel = await _unitOfWork.Novels.GetWithReferencesByIdAsync(id, cancellationToken);

        if (novel is null)
        {
            throw new ApiException(ErrorCodes.NovelNotFound, $"Novel with ID {id} was not found.");
        }
        
        return novel;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Novel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Novels.GetAllAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task CreateAsync(Novel novel, CancellationToken cancellationToken = default)
    {
        _unitOfWork.Novels.Add(novel);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Novel novel, CancellationToken cancellationToken = default)
    {
        _unitOfWork.Novels.Update(novel);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var novel = await GetByIdAsync(id, cancellationToken);
        
        _unitOfWork.Novels.Remove(novel);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        DeleteCoverImage(id);
    }

    /// <inheritdoc />
    public async Task<Prose> GetProseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Prose is stored as a JSON file instead of a database JSON column
        // to be kinder to the database and to allow for easier editing
        // through a fully fledged text editor if needed (e.g., batch replace).
        var path = Path.Combine(_dataFolder, "novels", id.ToString(), "prose.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        
        string proseJson;
        
        if (!File.Exists(path))
        {
            var prose = new Prose();
            proseJson = JsonSerializer.Serialize(prose, _jsonSerializerOptions);
            await File.WriteAllTextAsync(path, proseJson, cancellationToken);
            return prose;
        }
        
        proseJson = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<Prose>(proseJson, _jsonSerializerOptions)!;
    }

    /// <inheritdoc />
    public async Task UpdateProseAsync(Guid id, Prose prose, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_dataFolder, "novels", id.ToString(), "prose.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        
        var proseJson = JsonSerializer.Serialize(prose, _jsonSerializerOptions);
        await File.WriteAllTextAsync(path, proseJson, cancellationToken);
    }

    private string? GetLocalCoverImageFilePath(Guid id)
    {
        // This is the folder where the cover is stored
        var folder = Path.Combine(_staticFilesRoot, "novels", id.ToString());
        
        if (!Directory.Exists(folder))
        {
            return null;
        }
        
        // Find the cover image in the folder (called cover_{guid}.png) to prevent
        // caching issues when the cover is updated.
        var coverFiles = Directory.GetFiles(folder, "cover_*.png");
        
        return coverFiles.Length == 0 ? null : coverFiles[0];
    }

    /// <inheritdoc />
    public string? GetCoverImageLocation(Guid id)
    {
        var localPath = GetLocalCoverImageFilePath(id);
        
        if (localPath is null)
        {
            return null;
        }
        
        return Path.Combine("static", "novels", id.ToString(), 
            Path.GetFileName(localPath));
    }

    /// <inheritdoc />
    public async Task UploadCoverImageAsync(Guid id, IFormFile file, CancellationToken cancellationToken = default)
    {   
        if (!await _unitOfWork.Novels.ExistsAsync(id, cancellationToken))
        {
            throw new ApiException(ErrorCodes.NovelNotFound, $"Novel with ID {id} was not found.");
        }
        
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);
        var imageBytes = memoryStream.ToArray();
        
        // If it's not a PNG file, convert it to PNG using ImageSharp.
        if (file.ContentType != "image/png")
        {
            using var image = Image.Load(imageBytes);
            using var outputStream = new MemoryStream();
            await image.SaveAsPngAsync(outputStream, cancellationToken);
            imageBytes = outputStream.ToArray();
        }
        
        // Delete the existing cover
        var existingCoverPath = GetLocalCoverImageFilePath(id);
        
        var path = Path.Combine(_staticFilesRoot, "novels", id.ToString(), $"cover_{Guid.NewGuid()}.png");
        
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, imageBytes, cancellationToken);
        
        if (existingCoverPath is not null)
        {
            File.Delete(existingCoverPath);
        }
    }

    /// <inheritdoc />
    public void DeleteCoverImage(Guid id)
    {
        var folder = Path.Combine(_staticFilesRoot, "novels", id.ToString());

        if (!Directory.Exists(folder))
        {
            return;
        }

        var coverFiles = Directory.GetFiles(folder, "cover_*.png");

        foreach (var coverFile in coverFiles)
        {
            File.Delete(coverFile);
        }
    }

    /// <inheritdoc />
    public async Task<string> UploadProseImageAsync(Guid id, IFormFile file, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_staticFilesRoot, "novels", id.ToString(), "prose-images");
        Directory.CreateDirectory(path);
        
        var filePath = Path.Combine(path, $"{Guid.NewGuid()}.png");
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);
        var imageBytes = memoryStream.ToArray();
        
        // Convert to PNG using ImageSharp.
        if (file.ContentType != "image/png")
        {
            using var image = Image.Load(imageBytes);
            using var outputStream = new MemoryStream();
            await image.SaveAsPngAsync(outputStream, cancellationToken);
            imageBytes = outputStream.ToArray();
        }
        
        await File.WriteAllBytesAsync(filePath, imageBytes, cancellationToken);
        return Path.GetFileName(filePath);
    }

    /// <inheritdoc />
    public async Task DeleteProseImageAsync(Guid id, string fileName, CancellationToken cancellationToken = default)
    {
        if (!await _unitOfWork.Novels.ExistsAsync(id, cancellationToken))
        {
            throw new ApiException(ErrorCodes.NovelNotFound, $"Novel with ID {id} was not found.");
        }

        var normalizedFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(normalizedFileName) ||
            !string.Equals(normalizedFileName, fileName, StringComparison.Ordinal))
        {
            throw new ApiException(ErrorCodes.BadRequest, "Invalid prose image file name.");
        }

        var filePath = Path.Combine(_staticFilesRoot, "novels", id.ToString(), "prose-images", normalizedFileName);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}
