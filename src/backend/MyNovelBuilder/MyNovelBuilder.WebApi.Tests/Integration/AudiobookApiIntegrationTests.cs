using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
using MyNovelBuilder.WebApi.Models.Novels;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Services.Tts;
using MyNovelBuilder.WebApi.Tests.Factories;
using NAudio.Wave;

namespace MyNovelBuilder.WebApi.Tests.Integration;

public sealed class AudiobookApiIntegrationTests
{
    [Fact]
    public async Task ApiStartsHostedJob_ReturnsDurableStatusAndIdempotentLocation_AndRejectsInvalidInput()
    {
        using var baseFactory = new TestWebApplicationFactory<Program>();
        var tts = new Provider();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddKeyedSingleton<ITtsService>(TtsProvider.ElevenLabs, tts);
            // Exercise the actual hosted lifecycle; the base fixture disables it for database-reset tests.
            services.AddHostedService<AudiobookWorker>();
        }));
        using var client = factory.CreateClient();
        var novelId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Novels.Add(new Novel { Id = novelId, Title = "API audiobook" });
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<INovelService>().UpdateProseAsync(novelId,
                new Prose { Chapters = [new Chapter { Title = "One", Sections = [new Section { Text = "<p>Test speech.</p>" }] }] });
        }
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        var root = $"/api/novel/{novelId}/audiobook";
        var invalid = await client.PostAsJsonAsync(root + "/jobs", new StartAudiobookJobRequest(Guid.Empty, ""));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var previewResponse = await client.PostAsJsonAsync(root + "/preview",
            new AudiobookSettingsRequest(TtsProvider.ElevenLabs, "model", "voice", false, false, 0));
        previewResponse.EnsureSuccessStatusCode();
        var preview = (await previewResponse.Content.ReadFromJsonAsync<AudiobookPreview>(options))!;
        Assert.Equal(0, tts.Calls);
        var start = new StartAudiobookJobRequest(preview.SnapshotId, "http-idempotency");
        var first = await client.PostAsJsonAsync(root + "/jobs", start);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.NotNull(first.Headers.Location);
        var job = (await first.Content.ReadFromJsonAsync<AudiobookJobStatus>(options))!;
        var repeat = await client.PostAsJsonAsync(root + "/jobs", start);
        Assert.Equal(first.Headers.Location, repeat.Headers.Location);
        using var refreshedClient = factory.CreateClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (job.State is "queued" or "running")
        {
            await Task.Delay(25, timeout.Token);
            job = (await refreshedClient.GetFromJsonAsync<AudiobookJobStatus>(first.Headers.Location, options, timeout.Token))!;
        }
        Assert.Equal("completed", job.State);
        Assert.Equal("ready", job.Stage);
        Assert.True(job.DownloadAvailable);
        Assert.Equal(1, tts.Calls);
        var downloadUrl = root + $"/jobs/{job.Id}/download";
        var download = await client.GetAsync(downloadUrl);
        download.EnsureSuccessStatusCode();
        Assert.Equal("application/zip", download.Content.Headers.ContentType!.MediaType);
        Assert.EndsWith("-audiobook.zip", download.Content.Headers.ContentDisposition!.FileNameStar);
        using (var bytes = new MemoryStream(await download.Content.ReadAsByteArrayAsync()))
        using (var zip = new System.IO.Compression.ZipArchive(bytes))
            Assert.Equal(new[] { "001-One.wav", "manifest.json" }, zip.Entries.Select(e => e.FullName));
        using var range = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        range.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 9);
        var partial = await client.SendAsync(range);
        Assert.Equal(HttpStatusCode.PartialContent, partial.StatusCode);
        Assert.Equal(10, (await partial.Content.ReadAsByteArrayAsync()).Length);
        var wrongDownload = await client.GetAsync($"/api/novel/{Guid.NewGuid()}/audiobook/jobs/{job.Id}/download");
        Assert.False(wrongDownload.IsSuccessStatusCode);
        var list = await refreshedClient.GetFromJsonAsync<AudiobookJobStatus[]>(root + "/jobs", options);
        Assert.Contains(list!, j => j.Id == job.Id);
        var wrongNovel = await client.GetAsync($"/api/novel/{Guid.NewGuid()}/audiobook/jobs/{job.Id}");
        Assert.False(wrongNovel.IsSuccessStatusCode);
    }
    private sealed class Provider : ITtsService
    {
        public int Calls;
        public AudioFormat OutputAudioFormat => AudioFormat.Wav;
        public Task<byte[]> GenerateAudioAsync(TtsRequest request, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Calls);
            using var stream = new MemoryStream();
            using (var writer = new WaveFileWriter(stream, new WaveFormat(24000, 16, 1))) writer.Write(new byte[480], 0, 480);
            return Task.FromResult(stream.ToArray());
        }
        public Task<Stream> GenerateAudioStreamAsync(TtsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<TtsModelDto>> GetModelsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<TtsModelDto>>(
            [new() { ModelId = "model", Name = "Model", Voices = [new() { VoiceId = "voice", Name = "Voice", Language = WritingLanguage.English }] }]);
        public Task<decimal?> GetBalanceUsdAsync(CancellationToken cancellationToken = default) => Task.FromResult<decimal?>(null);
    }
}
