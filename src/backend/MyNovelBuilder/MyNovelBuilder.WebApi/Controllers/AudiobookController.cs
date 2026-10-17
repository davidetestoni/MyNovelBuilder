using Microsoft.AspNetCore.Mvc;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
using MyNovelBuilder.WebApi.Services;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>Saved-novel audiobook orchestration. Downloads are introduced with chapter packaging.</summary>
[ApiController]
[Route("api/novel/{novelId:guid}/audiobook")]
public sealed class AudiobookController(AudiobookJobService jobs) : ControllerBase
{
    /// <summary>Previews saved novel inputs and cache reuse without paid generation.</summary>
    [HttpPost("preview")]
    public Task<AudiobookPreview> Preview(Guid novelId, AudiobookSettingsRequest request, CancellationToken ct) => jobs.PreviewAsync(novelId, request, ct);
    /// <summary>Queues a preview for generation and returns its status URL.</summary>
    [HttpPost("jobs")]
    public async Task<ActionResult<AudiobookJobStatus>> Start(Guid novelId, StartAudiobookJobRequest request, CancellationToken ct)
    {
        var job = await jobs.StartAsync(novelId, request, ct);
        return AcceptedAtAction(nameof(Get), new { novelId, jobId = job.Id }, job);
    }
    /// <summary>Lists recent jobs for the novel, with active jobs first.</summary>
    [HttpGet("jobs")]
    public Task<IReadOnlyList<AudiobookJobStatus>> List(Guid novelId, CancellationToken ct) => jobs.ListAsync(novelId, ct);
    /// <summary>Returns progress for a job belonging to the novel.</summary>
    [HttpGet("jobs/{jobId:guid}")]
    public Task<AudiobookJobStatus> Get(Guid novelId, Guid jobId, CancellationToken ct) => jobs.GetAsync(novelId, jobId, ct);
    /// <summary>Requests cancellation while retaining reusable completed audio.</summary>
    [HttpPost("jobs/{jobId:guid}/cancel")]
    public Task<AudiobookJobStatus> Cancel(Guid novelId, Guid jobId, CancellationToken ct) => jobs.CancelAsync(novelId, jobId, ct);
    /// <summary>Retries failed or cancelled work after verifying its original inputs are available.</summary>
    [HttpPost("jobs/{jobId:guid}/retry")]
    public async Task<ActionResult<AudiobookJobStatus>> Retry(Guid novelId, Guid jobId, CancellationToken ct)
    {
        var job = await jobs.RetryAsync(novelId, jobId, ct);
        return AcceptedAtAction(nameof(Get), new { novelId, jobId }, job);
    }
}
