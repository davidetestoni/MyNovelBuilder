using System.Text.Json;
using System.Text.Json.Serialization;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.Novels;
using SixLabors.ImageSharp;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for novels.
/// </summary>
public class NovelService : INovelService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    /// <summary></summary>
    public NovelService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        
        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        _jsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
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
        
        DeleteCoverImage(id);
    }

    /// <inheritdoc />
    public async Task<Prose> GetProseAsync(Guid id)
    {
        // Prose is stored as a JSON file instead of a database JSON column
        // to be kinder to the database and to allow for easier editing
        // through a fully fledged text editor if needed (e.g., batch replace).
        var path = Path.Combine(Globals.DataFolder, "novels", id.ToString(), "prose.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        
        string proseJson;
        
        if (!File.Exists(path))
        {
            var prose = new Prose();
            proseJson = JsonSerializer.Serialize(prose, _jsonSerializerOptions);
            await File.WriteAllTextAsync(path, proseJson);
            return prose;
        }
        
        proseJson = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<Prose>(proseJson, _jsonSerializerOptions)!;
    }

    /// <inheritdoc />
    public async Task UpdateProseAsync(Guid id, Prose prose)
    {
        var path = Path.Combine(Globals.DataFolder, "novels", id.ToString(), "prose.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        
        var proseJson = JsonSerializer.Serialize(prose, _jsonSerializerOptions);
        await File.WriteAllTextAsync(path, proseJson);
    }

    private static string? GetLocalCoverImageFilePath(Guid id)
    {
        // This is the folder where the cover is stored
        var folder = Path.Combine(Globals.StaticFilesRoot, "novels", id.ToString());
        
        if (!Directory.Exists(folder))
        {
            return null;
        }
        
        // Find the cover image in the folder (called cover_{guid}.png) to prevent
        // caching issues when the cover is updated.
        var coverFiles = Directory.GetFiles(folder, "cover*.png");
        
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
    public async Task UploadCoverImageAsync(Guid id, IFormFile file)
    {   
        if (!await _unitOfWork.Novels.ExistsAsync(id))
        {
            throw new ApiException(ErrorCodes.NovelNotFound, $"Novel with ID {id} was not found.");
        }
        
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
        
        // Delete the existing cover
        var existingCoverPath = GetLocalCoverImageFilePath(id);
        
        var path = Path.Combine(Globals.StaticFilesRoot, "novels", id.ToString(), $"cover_{Guid.NewGuid()}.png");
        
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, imageBytes);
        
        if (existingCoverPath is not null)
        {
            File.Delete(existingCoverPath);
        }
    }

    /// <inheritdoc />
    public void DeleteCoverImage(Guid id)
    {
        var path = Path.Combine(Globals.StaticFilesRoot, "novels", id.ToString(), "cover.png");
        
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
