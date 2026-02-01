using System.Net.WebSockets;
using System.Text.Json;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Services.Tts;

/// <summary>
/// Text-to-speech service for VibeVoice.
/// </summary>
public class VibeVoiceTtsService : ITtsService
{
    private readonly HttpClient _httpClient;
    private readonly IIntegrationsService _integrationsService;

    // TODO: Read this from config
    private const string _host = "localhost:8000";
    
    /// <inheritdoc/>
    public bool SupportsEmphasisTags => false;
    
    /// <inheritdoc />
    public AudioFormat OutputAudioFormat => AudioFormat.Wav;

    /// <summary></summary>
    public VibeVoiceTtsService(
        HttpClient httpClient,
        IIntegrationsService integrationsService)
    {
        _httpClient = httpClient;
        _integrationsService = integrationsService;
    }
    
    /// <inheritdoc/>
    public async Task<byte[]> GenerateAudioAsync(TtsRequestDto request)
    {
        var config = await _integrationsService.GetConfigAsync();
        
        // The websocket accepts query parameters for text and voice
        var uriBuilder = new UriBuilder($"ws://{_host}/stream");
        var query = System.Web.HttpUtility.ParseQueryString(string.Empty);
        query["text"] = request.Message;
        query["voice"] = config.TtsVoiceId;
        uriBuilder.Query = query.ToString();
        
        using var ws = new ClientWebSocket();
        
        // TODO: Pass cancellation token down from higher level
        var ct = CancellationToken.None;
        await ws.ConnectAsync(uriBuilder.Uri, ct);
        
        var buffer = new byte[64 * 1024];
        using var audioData = new MemoryStream(); // 16-bit PCM, little-endian, 24kHz

        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            WebSocketReceiveResult result;
            var messageBuffer = new ArraySegment<byte>(buffer);
            using var ms = new MemoryStream();

            do
            {
                result = await ws.ReceiveAsync(messageBuffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", ct);
                    return [];
                }

                await ms.WriteAsync(buffer.AsMemory(0, result.Count), ct);
            } while (!result.EndOfMessage);

            ms.Position = 0;

            if (result.MessageType == WebSocketMessageType.Text)
            {
                using var reader = new StreamReader(ms);
                var messageJson = await reader.ReadToEndAsync(ct);
                using var document = JsonDocument.Parse(messageJson);
                
                if (!document.RootElement.TryGetProperty("event", out var eventElement))
                {
                    continue;
                }
                
                // If "event" is backend_stream_complete, we are done
                if (eventElement.GetString() == "backend_stream_complete")
                {
                    break;
                }
            }
            else if (result.MessageType == WebSocketMessageType.Binary)
            {
                await ms.CopyToAsync(audioData, ct);
            }
        }
        
        // The bytes aren't in wav format, so we need to encode them
        audioData.Seek(0, SeekOrigin.Begin);
        var audioBytes = audioData.ToArray();
        using var finalAudio = new MemoryStream();
        await using var writer = new NAudio.Wave.WaveFileWriter(finalAudio, 
            new NAudio.Wave.WaveFormat(24000, 16, 1));
        
        await writer.WriteAsync(audioBytes, ct);
        
        finalAudio.Seek(0, SeekOrigin.Begin);
        
        return finalAudio.ToArray();
    }
    
    /// <inheritdoc />
    public async Task<Stream> GenerateAudioStreamAsync(TtsRequestDto request)
    {
        var config = await _integrationsService.GetConfigAsync();
        
        var uriBuilder = new UriBuilder($"ws://{_host}/stream");
        var query = System.Web.HttpUtility.ParseQueryString(string.Empty);
        query["text"] = request.Message;
        query["voice"] = config.TtsVoiceId;
        uriBuilder.Query = query.ToString();
        
        var ws = new ClientWebSocket();
        var ct = CancellationToken.None;
        await ws.ConnectAsync(uriBuilder.Uri, ct);
        
        return new WebSocketToWavStream(ws, ct);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<TtsVoiceDto>> GetVoicesAsync()
    {
        var response = await _httpClient.GetAsync($"http://{_host}/config");
        response.EnsureSuccessStatusCode();

        // The returned JSON is in the format:
        // { "voices": ["voice1", "voice2", ...], ... }
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var voices = document.RootElement.GetProperty("voices").EnumerateArray()
            .Select(v => new TtsVoiceDto
            {
                Name = v.GetString()!,
                VoiceId = v.GetString()!,
            })
            .ToList();
        
        return voices;
    }
    
    /// <summary>
    /// Stream that reads PCM audio data from the WebSocket and outputs a WAV stream.
    /// </summary>
    private sealed class WebSocketToWavStream : Stream
    {
        private readonly ClientWebSocket _ws;
        private readonly CancellationToken _ct;
        private readonly Queue<byte[]> _chunks = new();
        private byte[] _currentChunk = [];
        private int _currentChunkPosition = 0;
        private bool _headerWritten = false;
        private bool _isComplete = false;
        private readonly SemaphoreSlim _semaphore = new(0);
        private Task? _readTask;

        public WebSocketToWavStream(ClientWebSocket ws, CancellationToken ct)
        {
            _ws = ws;
            _ct = ct;
            
            // Start reading from WebSocket in background
            _readTask = Task.Run(ReadFromWebSocket, ct);
        }

        private async Task ReadFromWebSocket()
        {
            try
            {
                var buffer = new byte[64 * 1024];

                while (_ws.State == WebSocketState.Open && !_ct.IsCancellationRequested)
                {
                    WebSocketReceiveResult result;
                    var messageBuffer = new ArraySegment<byte>(buffer);
                    using var ms = new MemoryStream();

                    do
                    {
                        result = await _ws.ReceiveAsync(messageBuffer, _ct);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", _ct);
                            break;
                        }

                        await ms.WriteAsync(buffer.AsMemory(0, result.Count), _ct);
                    } while (!result.EndOfMessage);

                    ms.Position = 0;

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        using var reader = new StreamReader(ms);
                        var messageJson = await reader.ReadToEndAsync(_ct);
                        using var document = JsonDocument.Parse(messageJson);

                        if (document.RootElement.TryGetProperty("event", out var eventElement) &&
                            eventElement.GetString() == "backend_stream_complete")
                        {
                            break;
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        var pcmData = ms.ToArray();
                        lock (_chunks)
                        {
                            _chunks.Enqueue(pcmData);
                        }
                        _semaphore.Release();
                    }
                }
            }
            finally
            {
                _isComplete = true;
                _semaphore.Release(); // Unblock any waiting reads
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // First, write the WAV header
            if (!_headerWritten)
            {
                _headerWritten = true;
                var header = CreateWavHeader();
                var bytesToCopy = Math.Min(count, header.Length);
                Array.Copy(header, 0, buffer, offset, bytesToCopy);
                
                if (bytesToCopy < header.Length)
                {
                    // Store remaining header bytes
                    _currentChunk = header;
                    _currentChunkPosition = bytesToCopy;
                }
                
                return bytesToCopy;
            }

            var totalRead = 0;

            // Read from current chunk if available
            while (totalRead < count && _currentChunkPosition < _currentChunk.Length)
            {
                var bytesToCopy = Math.Min(count - totalRead, _currentChunk.Length - _currentChunkPosition);
                Array.Copy(_currentChunk, _currentChunkPosition, buffer, offset + totalRead, bytesToCopy);
                _currentChunkPosition += bytesToCopy;
                totalRead += bytesToCopy;
            }

            // If we've exhausted the current chunk and still have room, get next chunk
            while (totalRead < count)
            {
                byte[]? nextChunk = null;
                
                lock (_chunks)
                {
                    if (_chunks.Count > 0)
                    {
                        nextChunk = _chunks.Dequeue();
                    }
                }

                if (nextChunk != null)
                {
                    _currentChunk = nextChunk;
                    _currentChunkPosition = 0;

                    var bytesToCopy = Math.Min(count - totalRead, _currentChunk.Length);
                    Array.Copy(_currentChunk, 0, buffer, offset + totalRead, bytesToCopy);
                    _currentChunkPosition += bytesToCopy;
                    totalRead += bytesToCopy;
                }
                else
                {
                    // No chunks available, wait for more or completion
                    if (_isComplete)
                    {
                        break; // No more data coming
                    }

                    await _semaphore.WaitAsync(cancellationToken);
                    
                    if (_isComplete && _chunks.Count == 0)
                    {
                        break;
                    }
                }
            }

            return totalRead;
        }

        private static byte[] CreateWavHeader()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            
            // Use a very large size (max int) since we don't know final size
            const int maxSize = int.MaxValue - 44;
            
            // RIFF header
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + maxSize); // File size - 8
            writer.Write(new[] { 'W', 'A', 'V', 'E' });
            
            // fmt chunk
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16); // fmt chunk size
            writer.Write((short)1); // PCM format
            writer.Write((short)1); // Channels
            writer.Write(24000); // Sample rate
            writer.Write(48000); // Byte rate (24000 * 1 * 16/8)
            writer.Write((short)2); // Block align
            writer.Write((short)16); // Bits per sample
            
            // data chunk
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(maxSize);
            
            return ms.ToArray();
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _ws.Dispose();
                _semaphore.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
