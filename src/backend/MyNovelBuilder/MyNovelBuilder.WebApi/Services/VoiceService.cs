using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Options;
using NAudio.Wave;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for voices.
/// </summary>
public class VoiceService : IVoiceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly string _dataFolder;

    /// <summary></summary>
    public VoiceService(
        IUnitOfWork unitOfWork,
        IOptions<AppStorageOptions> storageOptions)
    {
        _unitOfWork = unitOfWork;
        _dataFolder = storageOptions.Value.DataFolder;
    }

    /// <inheritdoc />
    public async Task<Voice> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var voice = await _unitOfWork.Voices.GetByIdAsync(id, cancellationToken);

        if (voice is null)
        {
            throw new ApiException(ErrorCodes.VoiceNotFound, $"Voice with ID {id} was not found.");
        }

        return voice;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Voice>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Voices.GetAllAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task CreateAsync(Voice voice, IFormFile wavFile, CancellationToken cancellationToken = default)
    {
        _unitOfWork.Voices.Add(voice);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await SaveWavFileAsync(voice.Id, wavFile, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(
        Voice voice,
        IFormFile? wavFile,
        CancellationToken cancellationToken = default)
    {
        _unitOfWork.Voices.Update(voice);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (wavFile is not null)
        {
            await SaveWavFileAsync(voice.Id, wavFile, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var voice = await GetByIdAsync(id, cancellationToken);
        _unitOfWork.Voices.Remove(voice);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        DeleteWavFile(id);
    }
    
    /// <inheritdoc />
    public async Task<byte[]> GetPreviewAsync(
        Guid id,
        int previewSeconds = 5,
        CancellationToken cancellationToken = default)
    {
        if (previewSeconds <= 0)
        {
            throw new ApiException(ErrorCodes.BadRequest, "Preview seconds must be greater than 0.");
        }

        _ = await GetByIdAsync(id, cancellationToken);
        var path = GetWavPath(id);

        if (!File.Exists(path))
        {
            throw new ApiException(ErrorCodes.InvalidFile, $"Voice sample for ID {id} was not found.");
        }

        await using var inputStream = File.OpenRead(path);
        await using var reader = new WaveFileReader(inputStream);
        await using var output = new MemoryStream();
        await using var writer = new WaveFileWriter(output, reader.WaveFormat);

        var maxBytes = previewSeconds * reader.WaveFormat.AverageBytesPerSecond;
        var bytesToCopy = Math.Min((int)reader.Length, maxBytes);
        var alignedBytesToCopy = bytesToCopy - (bytesToCopy % reader.WaveFormat.BlockAlign);
        var remaining = alignedBytesToCopy;
        var buffer = new byte[16 * 1024];

        while (remaining > 0)
        {
            var readSize = Math.Min(buffer.Length, remaining);
            var read = await reader.ReadAsync(buffer.AsMemory(0, readSize), cancellationToken);

            if (read == 0)
            {
                break;
            }

            await writer.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            remaining -= read;
        }

        return output.ToArray();
    }

    private async Task SaveWavFileAsync(Guid id, IFormFile wavFile, CancellationToken cancellationToken = default)
    {
        var path = GetWavPath(id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var stream = wavFile.OpenReadStream();
        await using var output = File.Create(path);
        await stream.CopyToAsync(output, cancellationToken);
    }

    private void DeleteWavFile(Guid id)
    {
        var path = GetWavPath(id);

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string GetWavPath(Guid id)
    {
        return Path.Combine(_dataFolder, "voices", $"{id}.wav");
    }
}
