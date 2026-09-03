using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.MediaLibrary;
using Microsoft.AspNetCore.StaticFiles;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for linked media folders.
/// </summary>
public class MediaFolderService : IMediaFolderService
{
    private readonly IUnitOfWork _unitOfWork;
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

    /// <summary></summary>
    public MediaFolderService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<MediaFolder> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var mediaFolder = await _unitOfWork.MediaFolders.GetByIdAsync(id, cancellationToken);

        if (mediaFolder is null)
        {
            throw new ApiException(
                ErrorCodes.MediaFolderNotFound,
                $"Media folder with ID {id} was not found.");
        }

        return mediaFolder;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<MediaFolder>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var mediaFolders = await _unitOfWork.MediaFolders.GetAllAsync(cancellationToken);
        return mediaFolders
            .OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(folder => folder.Path, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task CreateAsync(MediaFolder mediaFolder, CancellationToken cancellationToken = default)
    {
        mediaFolder.Name = mediaFolder.Name.Trim();
        mediaFolder.Path = Path.GetFullPath(mediaFolder.Path.Trim());

        if (!Directory.Exists(mediaFolder.Path))
        {
            throw new ApiException(
                ErrorCodes.BadRequest,
                $"The folder '{mediaFolder.Path}' does not exist.");
        }

        var existingFolder = await _unitOfWork.MediaFolders.GetByPathAsync(
            mediaFolder.Path,
            cancellationToken);

        if (existingFolder is not null)
        {
            throw new ApiException(
                ErrorCodes.BadRequest,
                $"The folder '{mediaFolder.Path}' is already linked.");
        }

        _unitOfWork.MediaFolders.Add(mediaFolder);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var mediaFolder = await GetByIdAsync(id, cancellationToken);
        _unitOfWork.MediaFolders.Remove(mediaFolder);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MediaFileInfo> UploadMediaAsync(
        Guid id,
        string fileName,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var mediaFolder = await GetByIdAsync(id, cancellationToken);
        EnsureFolderExists(mediaFolder.Path);

        var sanitizedFileName = SanitizeFileName(fileName);
        var filePath = Path.Combine(mediaFolder.Path, sanitizedFileName);

        await using var input = file.OpenReadStream();
        await using var output = File.Create(filePath);
        await input.CopyToAsync(output, cancellationToken);
        await output.FlushAsync(cancellationToken);

        return GetMediaFileInfo(new FileInfo(filePath));
    }

    /// <inheritdoc />
    public async Task DeleteMediaAsync(
        Guid id,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var mediaFolder = await GetByIdAsync(id, cancellationToken);
        EnsureFolderExists(mediaFolder.Path);

        var sanitizedFileName = SanitizeFileName(fileName);
        var filePath = Path.Combine(mediaFolder.Path, sanitizedFileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<MediaFileInfo>> GetAllMediaAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var mediaFolder = await GetByIdAsync(id, cancellationToken);
        EnsureFolderExists(mediaFolder.Path);

        // Only include files directly inside the linked folder, not subfolders.
        return await Task.FromResult(new DirectoryInfo(mediaFolder.Path)
            .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
            .Select(GetMediaFileInfo)
            .OrderByDescending(file => file.LastModifiedAt)
            .ThenBy(file => file.FileName, StringComparer.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<MediaFileContent> GetMediaAsync(
        Guid id,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var mediaFolder = await GetByIdAsync(id, cancellationToken);
        EnsureFolderExists(mediaFolder.Path);

        var sanitizedFileName = SanitizeFileName(fileName);
        var filePath = Path.Combine(mediaFolder.Path, sanitizedFileName);

        if (!File.Exists(filePath))
        {
            throw new ApiException(
                ErrorCodes.BadRequest,
                $"The media file '{sanitizedFileName}' was not found.");
        }

        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        var contentType = ContentTypeProvider.TryGetContentType(sanitizedFileName, out var resolvedContentType)
            ? resolvedContentType
            : "application/octet-stream";

        return new MediaFileContent
        {
            FileName = sanitizedFileName,
            ContentType = contentType,
            Bytes = bytes,
        };
    }

    private static MediaFileInfo GetMediaFileInfo(FileInfo fileInfo)
    {
        return new MediaFileInfo
        {
            FileName = fileInfo.Name,
            LastModifiedAt = fileInfo.LastWriteTimeUtc,
            SizeBytes = fileInfo.Length,
        };
    }

    private static void EnsureFolderExists(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new ApiException(
                ErrorCodes.BadRequest,
                $"The folder '{folderPath}' does not exist.");
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var trimmed = Path.GetFileName(fileName.Trim());
        var extension = Path.GetExtension(trimmed);
        var baseName = Path.GetFileNameWithoutExtension(trimmed).Trim();

        if (string.IsNullOrWhiteSpace(baseName) || string.IsNullOrWhiteSpace(extension))
        {
            throw new ApiException(
                ErrorCodes.BadRequest,
                "The media file name must include a base name and extension.");
        }

        var invalidChars = "<>:\"/\\|?*".ToHashSet();
        var sanitizedBaseName = new string(baseName
            .Select(ch => char.IsControl(ch) || invalidChars.Contains(ch) ? '_' : ch)
            .ToArray())
            .Trim('.', ' ');
        var sanitizedExtension = new string(extension
            .Select(ch => char.IsControl(ch) || invalidChars.Contains(ch) ? '_' : ch)
            .ToArray());

        if (string.IsNullOrWhiteSpace(sanitizedBaseName))
        {
            throw new ApiException(
                ErrorCodes.BadRequest,
                "The media file name is invalid after sanitization.");
        }

        return $"{sanitizedBaseName}{sanitizedExtension}";
    }
}
