using System.Net.WebSockets;
using System.Text.Json;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.Tts;

using MyNovelBuilder.WebApi.Attributes;
using MyNovelBuilder.WebApi.Helpers;

namespace MyNovelBuilder.WebApi.Services.Tts;

/// <summary>
/// Text-to-speech service for VibeVoice.
/// </summary>
[RegisterKeyedService(TtsProvider.VibeVoice, useHttpClient: true)]
public class VibeVoiceTtsService : ITtsService
{
    private readonly HttpClient _httpClient;

    // TODO: Read this from config
    private const string _host = "localhost:8000";
    
    /// <inheritdoc />
    public AudioFormat OutputAudioFormat => AudioFormat.Wav;

    /// <summary></summary>
    public VibeVoiceTtsService(
        HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    /// <inheritdoc/>
    public async Task<byte[]> GenerateAudioAsync(
        TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        // The websocket accepts query parameters for text and voice
        var uriBuilder = new UriBuilder($"ws://{_host}/stream");
        var query = System.Web.HttpUtility.ParseQueryString(string.Empty);
        query["text"] = request.Message;
        query["voice"] = request.VoiceId;
        uriBuilder.Query = query.ToString();
        
        using var ws = new ClientWebSocket();
        
        var ct = cancellationToken;
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
    public async Task<Stream> GenerateAudioStreamAsync(
        TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        var uriBuilder = new UriBuilder($"ws://{_host}/stream");
        var query = System.Web.HttpUtility.ParseQueryString(string.Empty);
        query["text"] = request.Message;
        query["voice"] = request.VoiceId;
        uriBuilder.Query = query.ToString();
        
        var ws = new ClientWebSocket();
        await ws.ConnectAsync(uriBuilder.Uri, cancellationToken);

        return new PcmWavStreamingStream(
            sampleRate: 24000,
            channels: 1,
            bitsPerSample: 16,
            producer: async (writeAsync, ct) =>
            {
                var buffer = new byte[64 * 1024];

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
                            return;
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

                        if (eventElement.GetString() == "backend_stream_complete")
                        {
                            break;
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        await writeAsync(ms.ToArray());
                    }
                }
            },
            ct: cancellationToken,
            onDispose: () => ws.Dispose());
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<TtsModelDto>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"http://{_host}/config", cancellationToken);
        response.EnsureSuccessStatusCode();

        // The returned JSON is in the format:
        // { "voices": ["voice1", "voice2", ...], ... }
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(json);
        var voices = document.RootElement.GetProperty("voices").EnumerateArray()
            .Select(v => new TtsVoiceDto
            {
                Name = v.GetString()!,
                VoiceId = v.GetString()!,
                Language = WritingLanguage.English
            })
            .ToList();

        return
        [
            new TtsModelDto
            {
                ModelId = "VibeVoice-Realtime-0.5B",
                Name = "VibeVoice Realtime 0.5B",
                Voices = voices
            }
        ];
    }

    /// <inheritdoc />
    public Task<decimal?> GetBalanceUsdAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<decimal?>(null);
}
