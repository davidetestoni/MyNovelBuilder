using Mapster;
using Microsoft.AspNetCore.Mvc;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.MediaLibrary;
using MyNovelBuilder.WebApi.Services;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>
/// Controller for the media library.
/// </summary>
[Route("api/media-library/folder")]
[ApiController]
public class MediaLibraryController : ControllerBase
{
    private readonly IMediaFolderService _mediaFolderService;

    /// <summary></summary>
    public MediaLibraryController(IMediaFolderService mediaFolderService)
    {
        _mediaFolderService = mediaFolderService;
    }

    /// <summary>
    /// Get all linked media folders.
    /// </summary>
    [HttpGet("/api/media-library/folders")]
    public async Task<IEnumerable<MediaFolderDto>> GetAllFolders(
        CancellationToken cancellationToken = default)
    {
        var folders = await _mediaFolderService.GetAllAsync(cancellationToken);
        return folders.Adapt<IEnumerable<MediaFolderDto>>();
    }

    /// <summary>
    /// Link a local media folder.
    /// </summary>
    [HttpPost]
    public async Task<MediaFolderDto> CreateFolder(
        CreateMediaFolderDto createMediaFolderDto,
        CancellationToken cancellationToken = default)
    {
        var mediaFolder = createMediaFolderDto.Adapt<MediaFolder>();
        await _mediaFolderService.CreateAsync(mediaFolder, cancellationToken);
        return mediaFolder.Adapt<MediaFolderDto>();
    }

    /// <summary>
    /// Unlink a local media folder by its ID.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task DeleteFolder(Guid id, CancellationToken cancellationToken = default)
    {
        await _mediaFolderService.DeleteAsync(id, cancellationToken);
    }

    /// <summary>
    /// Upload a media file into a linked folder.
    /// </summary>
    [HttpPost("{id:guid}/media")]
    public async Task<MediaFileDto> UploadMedia(
        Guid id,
        [FromForm] UploadMediaDto uploadMediaDto,
        CancellationToken cancellationToken = default)
    {
        var media = await _mediaFolderService.UploadMediaAsync(
            id,
            uploadMediaDto.Name,
            uploadMediaDto.File,
            cancellationToken);

        return media.Adapt<MediaFileDto>();
    }

    /// <summary>
    /// Delete a media file from a linked folder by file name.
    /// </summary>
    [HttpDelete("{id:guid}/media/{fileName}")]
    public async Task DeleteMedia(
        Guid id,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        await _mediaFolderService.DeleteMediaAsync(id, fileName, cancellationToken);
    }

    /// <summary>
    /// Get all top-level media files from a linked folder.
    /// </summary>
    [HttpGet("{id:guid}/media")]
    public async Task<IEnumerable<MediaFileDto>> GetAllMedia(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var media = await _mediaFolderService.GetAllMediaAsync(id, cancellationToken);
        return media.Adapt<IEnumerable<MediaFileDto>>();
    }

    /// <summary>
    /// Get a media file from a linked folder.
    /// </summary>
    [HttpGet("{id:guid}/media/{fileName}")]
    public async Task<IActionResult> GetMedia(
        Guid id,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var media = await _mediaFolderService.GetMediaAsync(id, fileName, cancellationToken);
        return File(media.Bytes, media.ContentType);
    }
}
