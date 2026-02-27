using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.Tts;

using MyNovelBuilder.WebApi.Attributes;

namespace MyNovelBuilder.WebApi.Services.Tts;

/// <summary>
/// Text-to-speech service from unrealspeech.com.
/// </summary>
[RegisterKeyedService(TtsProvider.UnrealSpeech, useHttpClient: true)]
public class UnrealSpeechTtsService : ITtsService
{
    private readonly ILogger<UnrealSpeechTtsService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IIntegrationsService _integrationsService;

    private readonly string[] _voices =
    [
        // American Female
        "Autumn", "Melody", "Hannah", "Emily", "Ivy", "Kaitlyn", "Luna", "Willow", "Lauren", "Sierra",
    
        // American Male
        "Noah", "Jasper", "Caleb", "Ronan", "Ethan", "Daniel", "Zane",
    
        // Chinese Female
        "Mei", "Lian", "Ting", "Jing",
    
        // Chinese Male
        "Wei", "Jian", "Hao", "Sheng",
    
        // Spanish Female
        "Lucía",
    
        // Spanish Male
        "Mateo", "Javier",
    
        // French Female
        "Élodie",
    
        // Hindi Female
        "Ananya", "Priya",
    
        // Hindi Male
        "Arjun", "Rohan",
    
        // Italian Female
        "Giulia",
    
        // Italian Male
        "Luca",
    
        // Portuguese Female
        "Camila",
    
        // Portuguese Male
        "Thiago", "Rafael"
    ];

    /// <inheritdoc />
    public bool SupportsEmphasisTags(string voiceId) => false;
    
    /// <inheritdoc />
    public AudioFormat OutputAudioFormat => AudioFormat.Wav;
    
    /// <summary></summary>
    public UnrealSpeechTtsService(
        ILogger<UnrealSpeechTtsService> logger,
        HttpClient httpClient,
        IIntegrationsService integrationsService)
    {
        _logger = logger;
        _httpClient = httpClient;
        _integrationsService = integrationsService;
        _httpClient.BaseAddress = new Uri("https://api.v8.unrealspeech.com");
    }
    
    /// <inheritdoc />
    public async Task<byte[]> GenerateAudioAsync(
        TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        var apiKey = config.UnrealSpeechApiKey;
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ApiException(ErrorCodes.MissingOrInvalidServiceCredentials,
                "UnrealSpeech API key is missing.");
        }
        
        // Create the task
        var httpRequest = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri(_httpClient.BaseAddress!, "synthesisTasks"),
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    Text = request.Message,
                    VoiceId = request.VoiceId,
                    Bitrate = "320k",
                    AudioFormat = "mp3",
                    OutputFormat = "uri",
                    TimestampType = "sentence",
                    sync = true
                }), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
        
        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        
        var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("UnrealSpeech TTS generation failed: {Response}", jsonResponse);
            throw new ApiException(ErrorCodes.ExternalServiceError,
                $"UnrealSpeech refused to generate audio: {jsonResponse}");
        }

        var responseObject = JsonNode.Parse(jsonResponse)!;
        
        var outputUri = responseObject["SynthesisTask"]!["OutputUri"]!.ToString();

        // Wait for the audio to be generated and uploaded to S3
        // This service is pretty unreliable...
        await Task.Delay(15000, cancellationToken);
        
        // Read the audio from the output uri
        // We use a brand new HttpClient here, otherwise it has the
        // Authorization header set and the request fails
        // (although we should be using the IHttpClientFactory to create it...)
        using var httpClient = new HttpClient();
        using var audioResponse = await httpClient.GetAsync(outputUri, cancellationToken);
        
        if (!audioResponse.IsSuccessStatusCode)
        {
            throw new ApiException(ErrorCodes.ExternalServiceError,
                $"Failed to fetch the generated audio file from the output uri: {await audioResponse.Content.ReadAsStringAsync(cancellationToken)}");
        }
        
        return await audioResponse.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Stream> GenerateAudioStreamAsync(
        TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        // This endpoint only supports text up to 1000 characters
        var textChunks = new TextChunker(1000).ChunkText(request.Message).ToList();
        
        return new UnrealSpeechStreamingStream(
            _httpClient, request.VoiceId, textChunks);
    }
    
    /// <inheritdoc />
    public Task<IEnumerable<TtsVoiceDto>> GetVoicesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_voices.Select(v => new TtsVoiceDto
        {
            VoiceId = v,
            Name = v,
            Language = WritingLanguage.English
        }));
    }
    
    private sealed class UnrealSpeechStreamingStream : Stream
    {
        private readonly HttpClient _httpClient;
        private readonly string _voiceId;
        private readonly List<string> _textChunks;
        private int _currentChunkIndex;
        private Stream? _currentStream;
        private bool _headerWritten; // Track header across chunks

        /// <summary></summary>
        public UnrealSpeechStreamingStream(HttpClient httpClient, string voiceId, List<string> textChunks)
        {
            _httpClient = httpClient;
            _voiceId = voiceId;
            _textChunks = textChunks;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            while (true)
            {
                // If we have a current stream, try to read from it
                if (_currentStream != null)
                {
                    var bytesRead = await _currentStream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);

                    if (bytesRead > 0)
                    {
                        return bytesRead;
                    }

                    // Current stream exhausted, dispose and move to next chunk
                    await _currentStream.DisposeAsync();
                    _currentStream = null;
                    _currentChunkIndex++;
                }

                // No more chunks
                if (_currentChunkIndex >= _textChunks.Count)
                {
                    return 0;
                }

                // Stream next chunk
                var textChunk = _textChunks[_currentChunkIndex];

                var payload = new
                {
                    Text = textChunk,
                    VoiceId = _voiceId,
                    Codec = "pcm_s16le"
                };
                var jsonPayload = JsonSerializer.Serialize(payload);
                var httpRequest = new HttpRequestMessage
                {
                    RequestUri = new Uri(_httpClient.BaseAddress!, "stream"),
                    Method = HttpMethod.Post,
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };

                var response = await _httpClient.SendAsync(
                    httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new ApiException(ErrorCodes.ExternalServiceError,
                        $"UnrealSpeech refused to generate audio stream: {errorContent}");
                }

                var pcmStream = await response.Content.ReadAsStreamAsync(cancellationToken);

                // Only prepend WAV header on first chunk
                _currentStream = _headerWritten
                    ? pcmStream
                    : new PcmToWavStream(pcmStream, writeHeader: true);

                _headerWritten = true;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
            => ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _currentStream?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    private sealed class PcmToWavStream : Stream
    {
        private readonly Stream _pcmStream;
        private readonly bool _writeHeader;
        private bool _headerWritten;

        /// <summary></summary>
        public PcmToWavStream(Stream pcmStream, bool writeHeader = true)
        {
            _pcmStream = pcmStream;
            _writeHeader = writeHeader;
        }

        public override async Task<int> ReadAsync(
            byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (!_writeHeader || _headerWritten)
            {
                return await _pcmStream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
            }

            _headerWritten = true;
            var header = CreateWavHeader();
            var toCopy = Math.Min(count, header.Length);
            Array.Copy(header, 0, buffer, offset, toCopy);
            return toCopy;

        }

        public override int Read(byte[] buffer, int offset, int count)
            => ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

        private static byte[] CreateWavHeader()
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);

            const int maxDataSize = int.MaxValue - 44;

            // RIFF
            w.Write("RIFF"u8.ToArray());
            w.Write(36 + maxDataSize);
            w.Write("WAVE"u8.ToArray());

            // fmt
            w.Write("fmt "u8.ToArray());
            w.Write(16);              // PCM fmt chunk size
            w.Write((short)1);        // PCM
            w.Write((short)1);        // Mono
            w.Write(22050);           // Sample rate
            w.Write(44100);           // Byte rate (22050 * 2)
            w.Write((short)2);        // Block align
            w.Write((short)16);       // Bits per sample

            // data
            w.Write("data"u8.ToArray());
            w.Write(maxDataSize);

            return ms.ToArray();
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _pcmStream.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
