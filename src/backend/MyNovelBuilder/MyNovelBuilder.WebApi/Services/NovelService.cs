using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
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
        
        DeleteCoverImage(id);
    }

    /// <inheritdoc />
    public string? GetCoverImageLocation(Guid id)
    {
        var localPath = Path.Combine(Globals.StaticFilesRoot, "novels", id.ToString(), "cover.png");
        var urlPath = Path.Combine("static", "novels", id.ToString(), "cover.png");
        return File.Exists(localPath) ? urlPath : null;
    }

    /// <inheritdoc />
    public async Task UploadCoverImageAsync(Guid id, IFormFile file)
    {   
        if (!await _unitOfWork.Novels.ExistsAsync(id))
        {
            throw new ApiException(ErrorCodes.NovelNotFound, $"Novel with ID {id} was not found.");
        }
        
        // If not a png file, throw an exception
        // TODO: Support other image formats
        if (file.ContentType != "image/png")
        {
            throw new ApiException(ErrorCodes.InvalidCoverImage, "Cover image must be a PNG file.");
        }
        
        var path = Path.Combine(Globals.StaticFilesRoot, "novels", id.ToString(), "cover.png");
        
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        
        await using var stream = new FileStream(path, FileMode.Create);
        await file.CopyToAsync(stream);
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
