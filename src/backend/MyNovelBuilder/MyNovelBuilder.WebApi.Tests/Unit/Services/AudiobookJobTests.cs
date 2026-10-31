using System.Collections.Immutable;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
using MyNovelBuilder.WebApi.Models.Integrations;
using MyNovelBuilder.WebApi.Models.Novels;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Options;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Services.TextGeneration;
using MyNovelBuilder.WebApi.Services.Tts;
using NAudio.Wave;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public sealed class AudiobookJobTests
{
    [Fact]
    public async Task ExhaustedPlanningHasSpecificPublicCodeWithoutExposingProseInStatus()
    {
        using var f = await Fixture.Create();
        var job = await f.Start(await f.Preview());
        f.Tts.BeforeCall = (_, _) => Task.FromException(new ImmersivePlanningException(3, "Source excerpt: PRIVATE prose."));
        await f.Worker.RunNextAsync(default);
        var failed = await f.Status(job.Id);
        Assert.Equal("immersive_planning_invalid", failed.ErrorCode);
        Assert.DoesNotContain("PRIVATE", JsonSerializer.Serialize(failed));
        Assert.Contains(f.Logger.Entries, e => e.Error?.Message.Contains("PRIVATE", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task OlderSavedJobDefaultsToOneAndCanResumeWithHigherParallelism()
    {
        using var f = await Fixture.Create();
        var job = await f.Start(await f.Preview());
        await f.Call(s => s.CancelAsync(f.NovelId, job.Id, default));
        var path = Path.Combine(f.Folder, "audiobooks", f.NovelId.ToString(), "jobs", job.Id + ".json");
        var saved = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
        saved.Remove("MaxParallelSections");
        await File.WriteAllTextAsync(path, saved.ToJsonString());
        f.Restart();
        Assert.Equal(1, (await f.Status(job.Id)).MaxParallelSections);
        var retry = await f.Call(s => s.RetryAsync(f.NovelId, job.Id, default, 3));
        Assert.Equal(3, retry.MaxParallelSections);
        await f.Worker.RunNextAsync(default);
        Assert.Equal("completed", (await f.Status(job.Id)).State);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ParallelSectionsReachSelectedLimitAndPersistAllProgress(int parallelism)
    {
        using var f = await Fixture.Create();
        await f.SetProse("One.", "Two.", "Three.", "Four.", "Five.");
        var job = await f.Start(await f.Preview(), parallelism: parallelism);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Tts.BeforeCall = async (n, _) =>
        {
            if (n == parallelism) entered.TrySetResult();
            await release.Task;
        };
        var run = f.Worker.RunNextAsync(default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var running = await f.Status(job.Id);
            Assert.Equal(parallelism, running.MaxParallelSections);
            Assert.Equal(parallelism, running.Sections.Count(s => s.State == "running"));
            Assert.Equal(parallelism, f.Tts.Messages.Count);
            Assert.Equal(0, running.CompletedSections);
        }
        finally { release.TrySetResult(); }
        await run.WaitAsync(TimeSpan.FromSeconds(10));
        var complete = await f.Status(job.Id);
        Assert.Equal("completed", complete.State);
        Assert.Equal(5, complete.CompletedSections);
        Assert.Equal(0, complete.FailedSections);
        Assert.True(complete.DownloadAvailable);
        Assert.Equal(parallelism, f.Tts.MaximumActiveCalls);
        Assert.Equal(5, f.Tts.Messages.Count);
    }

    [Fact]
    public async Task ParallelCancellationDrainsAllCalls_AndRetryCanChangeAndRetainConcurrencyAfterRestart()
    {
        using var f = await Fixture.Create();
        await f.SetProse("One.", "Two.", "Three.", "Four.");
        var job = await f.Start(await f.Preview(), parallelism: 2);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Tts.BeforeCall = async (n, _) =>
        {
            if (n == 2) entered.TrySetResult();
            await release.Task;
        };
        var run = f.Worker.RunNextAsync(default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await f.Call(s => s.CancelAsync(f.NovelId, job.Id, default));
            Assert.False(run.IsCompleted);
            Assert.Equal("cancelling", (await f.Status(job.Id)).State);
        }
        finally { release.TrySetResult(); }
        await run.WaitAsync(TimeSpan.FromSeconds(10));
        var cancelled = await f.Status(job.Id);
        Assert.Equal("cancelled", cancelled.State);
        Assert.DoesNotContain(cancelled.Sections, s => s.State == "running");
        Assert.Equal(2, f.Tts.Messages.Count);
        f.Tts.BeforeCall = null;
        f.Restart();
        var retry = await f.Call(s => s.RetryAsync(f.NovelId, job.Id, default, 3));
        Assert.Equal(3, retry.MaxParallelSections);
        f.Restart();
        await f.Worker.RecoverAsync(default);
        retry = await f.Call(s => s.RetryAsync(f.NovelId, job.Id, default));
        Assert.Equal(3, retry.MaxParallelSections);
        await f.Worker.RunNextAsync(default);
        Assert.Equal("completed", (await f.Status(job.Id)).State);
    }

    [Fact]
    public async Task ParallelFailureIsLoggedAndLeavesNoRunningSections()
    {
        using var f = await Fixture.Create();
        await f.SetProse("One.", "Two.", "Three.", "Four.");
        var job = await f.Start(await f.Preview(), parallelism: 2);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fail = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Tts.BeforeCall = async (n, _) =>
        {
            if (n == 1) { await fail.Task; throw new InvalidOperationException("provider explanation"); }
            entered.TrySetResult();
            await release.Task;
        };
        var run = f.Worker.RunNextAsync(default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            fail.TrySetResult();
            // The other provider operation must still drain before this job can finish.
            Assert.False(run.IsCompleted);
        }
        finally { fail.TrySetResult(); release.TrySetResult(); }
        await run.WaitAsync(TimeSpan.FromSeconds(10));
        var failed = await f.Status(job.Id);
        Assert.Equal("failed", failed.State);
        Assert.Equal(1, failed.FailedSections);
        Assert.DoesNotContain(failed.Sections, s => s.State == "running");
        Assert.Contains(f.Logger.Entries, e => e.Error?.Message == "provider explanation");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public async Task InvalidParallelismIsRejectedForStartAndRetry(int parallelism)
    {
        using var f = await Fixture.Create();
        var preview = await f.Preview();
        await Assert.ThrowsAsync<ApiException>(() => f.Start(preview, parallelism: parallelism));
        var job = await f.Start(preview);
        await f.Call(s => s.CancelAsync(f.NovelId, job.Id, default));
        await Assert.ThrowsAsync<ApiException>(() => f.Call(s => s.RetryAsync(f.NovelId, job.Id, default, parallelism)));
        Assert.Equal(1, (await f.Status(job.Id)).MaxParallelSections);
    }

    [Fact]
    public async Task FrequentEstimatesEvictOldPreviewsButKeepSubmittedSnapshots()
    {
        using var f = await Fixture.Create();
        var preview = await f.Preview();
        var store = f.Services.GetRequiredService<AudiobookSnapshotStore>();
        var snapshot = store.Get(preview.SnapshotId, f.NovelId)!;
        await f.Start(preview);
        var oldest = store.Add(snapshot);
        Guid latest = oldest;
        for (var i = 0; i < 40; i++) latest = store.Add(snapshot);
        Assert.Null(store.Get(oldest, f.NovelId));
        Assert.NotNull(store.Get(latest, f.NovelId));
        Assert.NotNull(store.Get(preview.SnapshotId, f.NovelId));
    }

    [Fact]
    public async Task PreviewIsFree_StartIsIdempotent_ExecutionUsesFrozenInputs_RepeatReusesAudio()
    {
        using var f = await Fixture.Create();
        var preview = await f.Preview();
        Assert.Equal(2, preview.EligibleSections);
        Assert.Equal(1, preview.EligibleChapters);
        Assert.Equal(39, preview.TotalCharacters);
        Assert.Equal(6, preview.SpokenWords);
        Assert.Equal(1, preview.SkippedSections);
        Assert.Equal(1, preview.SkippedChapters);
        Assert.Empty(f.Tts.Messages);
        var request = new StartAudiobookJobRequest(preview.SnapshotId, "one");
        var jobs = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => f.Call(s => s.StartAsync(f.NovelId, request, default))));
        Assert.Single(jobs.Select(j => j.Id).Distinct());
        await Assert.ThrowsAsync<ApiException>(() => f.Call(s => s.StartAsync(f.NovelId, request with { IdempotencyKey = "two" }, default)));
        await f.SetProse("Changed saved prose.");
        Assert.True(await f.Worker.RunNextAsync(default));
        Assert.Equal(new[] { "First secret prose.", "Second secret prose." }, f.Tts.Messages);
        var status = await f.Status(jobs[0].Id);
        Assert.Equal("completed", status.State);
        Assert.Equal("ready", status.Stage);
        Assert.True(status.DownloadAvailable);
        Assert.Equal(2, status.GeneratedSections);
        Assert.Equal(1, status.CompletedChapters);
        Assert.Equal(status.Id, (await f.Call(s => s.StartAsync(f.NovelId, request, default))).Id);
        await f.SetProse("First secret prose.", "Second secret prose.", "");
        var second = await f.Preview();
        Assert.Equal(2, second.ReusableSections);
        Assert.Equal(6, second.SpokenWords);
        Assert.Equal(39, second.TotalCharacters);
        Assert.Equal(0, second.CharactersNeedingSynthesis);
        var repeat = await f.Start(second, "repeat");
        await f.Worker.RunNextAsync(default);
        Assert.Equal(2, (await f.Status(repeat.Id)).ReusedSections);
        Assert.Equal(2, f.Tts.Messages.Count);
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await File.ReadAllTextAsync(Path.Combine(f.Folder, "audiobooks", f.NovelId.ToString(), "jobs", jobs[0].Id + ".json"));
        Assert.DoesNotContain("secret prose", persisted);
        Assert.DoesNotContain("http", persisted);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task CompletedDownloadSurvivesRestart_MissingZipRetriesWithoutProviderCalls_AndNewFailureKeepsOldDownload()
    {
        using var f = await Fixture.Create();
        var first = await f.Start(await f.Preview());
        await Assert.ThrowsAsync<ApiException>(() => f.Call(s => s.DownloadAsync(f.NovelId, first.Id, default)));
        await f.Worker.RunNextAsync(default);
        f.Restart();
        await f.Worker.RecoverAsync(default);
        var download = await f.Call(s => s.DownloadAsync(f.NovelId, first.Id, default));
        await download.Stream.DisposeAsync();
        await Assert.ThrowsAsync<ApiException>(() => f.Call(s => s.RetryAsync(f.NovelId, first.Id, default)));
        var repository = f.Services.GetRequiredService<FileSystemAudiobookJobRepository>();
        var metadata = (await repository.ReadAsync(f.NovelId, first.Id))!;
        File.Delete(Path.Combine(f.Folder, "audiobooks", f.NovelId.ToString(), "archives", metadata.Archive!.Key + ".zip"));
        Assert.False((await f.Status(first.Id)).DownloadAvailable);
        await Assert.ThrowsAsync<ApiException>(() => f.Call(s => s.DownloadAsync(f.NovelId, first.Id, default)));
        await f.Call(s => s.RetryAsync(f.NovelId, first.Id, default));
        await f.Worker.RunNextAsync(default);
        Assert.True((await f.Status(first.Id)).DownloadAvailable);
        Assert.Equal(2, f.Tts.Messages.Count);
        await f.SetProse("Changed prose for a later run.");
        var later = await f.Start(await f.Preview(), "later");
        f.Tts.BeforeCall = (_, _) => Task.FromException(new InvalidOperationException());
        await f.Worker.RunNextAsync(default);
        Assert.Equal("failed", (await f.Status(later.Id)).State);
        Assert.False((await f.Status(later.Id)).DownloadAvailable);
        var previous = await f.Call(s => s.DownloadAsync(f.NovelId, first.Id, default));
        await previous.Stream.DisposeAsync();
    }

    [Fact]
    public async Task FailureAndRetryKeepCompletedAudioAndSnapshotDespiteSavedEdits()
    {
        using var f = await Fixture.Create();
        var preview = await f.Preview();
        var job = await f.Start(preview);
        f.Tts.BeforeCall = (n, _) => n == 2 ? Task.FromException(new InvalidOperationException("PRIVATE provider body")) : Task.CompletedTask;
        await f.Worker.RunNextAsync(default);
        var failed = await f.Status(job.Id);
        Assert.Equal("failed", failed.State);
        Assert.Equal(1, failed.CompletedSections);
        Assert.Equal(1, failed.FailedSections);
        Assert.DoesNotContain("PRIVATE", JsonSerializer.Serialize(failed));
        await f.SetProse("New content must not replace the snapshot.");
        f.Tts.BeforeCall = null;
        await f.Call(s => s.RetryAsync(f.NovelId, job.Id, default));
        await f.Worker.RunNextAsync(default);
        var completed = await f.Status(job.Id);
        Assert.Equal("completed", completed.State);
        Assert.Equal(2, completed.AttemptNumber);
        Assert.Equal(1, completed.ReusedSections);
        Assert.Equal(3, f.Tts.Messages.Count);
        Assert.Equal("Second secret prose.", f.Tts.Messages[^1]);
        Assert.Equal("failed", completed.Attempts[0].State);
    }

    [Fact]
    public async Task CancellationDrainsActiveCall_AndRetryRetainsTheFirstSection()
    {
        using var f = await Fixture.Create();
        var job = await f.Start(await f.Preview());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Tts.BeforeCall = async (n, _) =>
        {
            if (n == 2) { entered.SetResult(); await release.Task; }
        };
        var run = f.Worker.RunNextAsync(default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await f.Call(s => s.CancelAsync(f.NovelId, job.Id, default));
        Assert.False(run.IsCompleted);
        Assert.Equal("cancelling", (await f.Status(job.Id)).State);
        release.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("cancelled", (await f.Status(job.Id)).State);
        Assert.Equal(1, (await f.Status(job.Id)).CompletedSections);
        f.Tts.BeforeCall = null;
        await f.Call(s => s.RetryAsync(f.NovelId, job.Id, default));
        await f.Worker.RunNextAsync(default);
        Assert.Equal(1, (await f.Status(job.Id)).ReusedSections);
    }

    [Fact]
    public async Task RestartDoesNotRunQueuedWork_RetryVerifiesSavedInputs()
    {
        using var f = await Fixture.Create();
        var job = await f.Start(await f.Preview());
        f.Restart();
        await f.Worker.RecoverAsync(default);
        Assert.False(await f.Worker.RunNextAsync(default));
        Assert.Empty(f.Tts.Messages);
        Assert.Equal("interrupted_requires_verified_retry", (await f.Status(job.Id)).ErrorCode);
        await f.Call(s => s.RetryAsync(f.NovelId, job.Id, default));
        await f.Worker.RunNextAsync(default);
        Assert.Equal("completed", (await f.Status(job.Id)).State);
        var next = await f.Start(await f.Preview(), "new");
        f.Restart();
        await f.Worker.RecoverAsync(default);
        await f.SetProse("Changed since restart.");
        await Assert.ThrowsAsync<ApiException>(() => f.Call(s => s.RetryAsync(f.NovelId, next.Id, default)));
        Assert.Equal("failed", (await f.Status(next.Id)).State);
    }

    [Fact]
    public async Task ShutdownMarksRunningAttemptInterruptedAndKeepsCompletedSections()
    {
        using var f = await Fixture.Create();
        var job = await f.Start(await f.Preview());
        using var shutdown = new CancellationTokenSource();
        f.Tts.BeforeCall = (n, cancellationToken) =>
        {
            if (n == 2) { shutdown.Cancel(); cancellationToken.ThrowIfCancellationRequested(); }
            return Task.CompletedTask;
        };
        await f.Worker.RunNextAsync(shutdown.Token);
        var status = await f.Status(job.Id);
        Assert.Equal("failed", status.State);
        Assert.Equal("interrupted_requires_verified_retry", status.ErrorCode);
        Assert.Equal(1, status.CompletedSections);
    }

    [Fact]
    public async Task DeletingNovelCancelsActiveJob_DropsMetadataAndMemory_ButKeepsSharedAudio()
    {
        using var f = await Fixture.Create();
        var preview = await f.Preview();
        var job = await f.Start(preview);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Tts.BeforeCall = async (n, _) => { if (n == 2) { entered.SetResult(); await release.Task; } };
        var run = f.Worker.RunNextAsync(default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using (var scope = f.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<INovelService>().DeleteAsync(f.NovelId);
        release.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Null(f.Services.GetRequiredService<AudiobookSnapshotStore>().Get(preview.SnapshotId, f.NovelId));
        Assert.Empty(await f.Services.GetRequiredService<FileSystemAudiobookJobRepository>().ListAsync());
        Assert.False(Directory.Exists(Path.Combine(f.Folder, "audiobooks", f.NovelId.ToString())));
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(f.Folder, "audio", "v1"), "*.wav"));
        await Assert.ThrowsAsync<ApiException>(() => f.Status(job.Id));
    }

    [Fact]
    public async Task MissingCacheAfterPreviewIsRecheckedAndWrongNovelCannotAccessJobs()
    {
        using var f = await Fixture.Create();
        var job = await f.Start(await f.Preview());
        await f.Worker.RunNextAsync(default);
        var preview = await f.Preview();
        Assert.Equal(2, preview.ReusableSections);
        foreach (var path in Directory.GetFiles(Path.Combine(f.Folder, "audio", "v1"), "*.wav")) File.Delete(path);
        var repeat = await f.Start(preview, "new");
        await f.Worker.RunNextAsync(default);
        Assert.Equal(4, f.Tts.Messages.Count);
        Assert.Equal(2, (await f.Status(repeat.Id)).GeneratedSections);
        await Assert.ThrowsAsync<ApiException>(() => f.Call(s => s.GetAsync(Guid.NewGuid(), job.Id, default)));
        await Assert.ThrowsAsync<ApiException>(() => f.Call(s => s.CancelAsync(Guid.NewGuid(), job.Id, default)));
        await Assert.ThrowsAsync<ApiException>(() => f.Call(s => s.RetryAsync(Guid.NewGuid(), job.Id, default)));
    }

    [Fact]
    public async Task EmptyNovelAndExpiredPreviewFailBeforePaidCalls_QueuedCancelIsIdempotent()
    {
        using var f = await Fixture.Create();
        var preview = await f.Preview();
        f.Clock.Now += TimeSpan.FromMinutes(16);
        await Assert.ThrowsAsync<ApiException>(() => f.Start(preview));
        var job = await f.Start(await f.Preview());
        await f.Call(s => s.CancelAsync(f.NovelId, job.Id, default));
        Assert.Equal("cancelled", (await f.Call(s => s.CancelAsync(f.NovelId, job.Id, default))).State);
        Assert.False(await f.Worker.RunNextAsync(default));
        await f.SetProse("");
        await Assert.ThrowsAsync<ApiException>(() => f.Preview());
        Assert.Empty(f.Tts.Messages);
    }

    [Fact]
    public async Task BoundedTransientRetriesDoNotRepeatCompletedSections()
    {
        using var f = await Fixture.Create();
        var job = await f.Start(await f.Preview());
        f.Tts.BeforeCall = (n, _) => n == 2 ? Task.FromException(new RetryableProviderException(HttpStatusCode.ServiceUnavailable, TimeSpan.Zero)) : Task.CompletedTask;
        await f.Worker.RunNextAsync(default);
        Assert.Equal("completed", (await f.Status(job.Id)).State);
        Assert.Equal(new[] { "First secret prose.", "Second secret prose.", "Second secret prose." }, f.Tts.Messages);
    }

    [Fact]
    public async Task ConcurrentNovelJobsShareProviderGate_AndOneJobCannotBeClaimedTwice()
    {
        using var f = await Fixture.Create();
        var first = await f.Start(await f.Preview());
        var secondNovel = Guid.NewGuid();
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Novels.Add(new Novel { Id = secondNovel, Title = "Second" });
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<INovelService>().UpdateProseAsync(secondNovel,
                new Prose { Chapters = [new Chapter { Title = "Other", Sections = [new Section { Text = "Other novel prose." }] }] });
        }
        var preview = await f.Call(s => s.PreviewAsync(secondNovel, new(), default));
        var second = await f.Call(s => s.StartAsync(secondNovel, new(preview.SnapshotId, "other"), default));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int active = 0;
        f.Tts.BeforeCall = async (_, cancellationToken) =>
        {
            Assert.Equal(1, Interlocked.Increment(ref active));
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            Interlocked.Decrement(ref active);
        };
        var firstRun = f.Worker.RunNextAsync(default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var secondRun = f.Worker.RunNextAsync(default);
        Assert.False(await f.Worker.RunNextAsync(default));
        Assert.Single(f.Tts.Messages);
        release.SetResult();
        await Task.WhenAll(firstRun, secondRun).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("completed", (await f.Status(first.Id)).State);
        Assert.Equal("completed", (await f.Call(s => s.GetAsync(secondNovel, second.Id, default))).State);
        Assert.Equal(3, f.Tts.Messages.Count);
    }

    [Fact]
    public async Task CrashRecoveryRetainsSectionCheckpoints()
    {
        using var f = await Fixture.Create();
        var job = await f.Start(await f.Preview());
        f.Tts.BeforeCall = (n, _) => n == 2 ? Task.FromException(new IOException()) : Task.CompletedTask;
        await f.Worker.RunNextAsync(default);
        using (var scope = f.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<FileSystemAudiobookJobRepository>();
            var metadata = (await repository.ReadAsync(f.NovelId, job.Id))!;
            metadata.State = "running";
            await repository.SaveAsync(metadata);
        }
        f.Restart();
        await f.Worker.RecoverAsync(default);
        Assert.Equal(1, (await f.Status(job.Id)).CompletedSections);
        f.Tts.BeforeCall = null;
        await f.Call(s => s.RetryAsync(f.NovelId, job.Id, default));
        await f.Worker.RunNextAsync(default);
        Assert.Equal(1, (await f.Status(job.Id)).ReusedSections);
        Assert.Equal(3, f.Tts.Messages.Count);
    }

    [Fact]
    public async Task InterruptedMetadataWritePreservesPreviousJob_AndIgnoresTemporaryFiles()
    {
        using var f = await Fixture.Create();
        var job = await f.Start(await f.Preview());
        var repository = f.Services.GetRequiredService<FileSystemAudiobookJobRepository>();
        var metadata = (await repository.ReadAsync(f.NovelId, job.Id))!;
        metadata.State = "failed";
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.SaveAsync(metadata, cancelled.Token));
        Assert.Equal("queued", (await repository.ReadAsync(f.NovelId, job.Id))!.State);
        var folder = Path.Combine(f.Folder, "audiobooks", f.NovelId.ToString(), "jobs");
        Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
        await File.WriteAllTextAsync(Path.Combine(folder, "interrupted.tmp"), "{partial");
        Assert.Single(await repository.ListAsync());
    }

    [Fact]
    public async Task DamagedJobMetadataDoesNotRunPaidCallsOrBreakOtherJobs()
    {
        using var f = await Fixture.Create();
        var job = await f.Start(await f.Preview());
        var file = Path.Combine(f.Folder, "audiobooks", f.NovelId.ToString(), "jobs", job.Id + ".json");
        await File.WriteAllTextAsync(file, "{truncated");
        Assert.False(await f.Worker.RunNextAsync(default));
        Assert.Empty(f.Tts.Messages);
        var next = await f.Start(await f.Preview(), "fresh");
        await f.Worker.RunNextAsync(default);
        Assert.Equal("completed", (await f.Status(next.Id)).State);
    }

    [Fact]
    public async Task FailureLogIncludesOriginalExceptionAndSectionContext()
    {
        using var f = await Fixture.Create();
        var job = await f.Start(await f.Preview());
        var error = new ApiException(ErrorCodes.ExternalServiceError, "Provider says: voice abc is unavailable");
        f.Tts.BeforeCall = (_, _) => Task.FromException(error);

        await f.Worker.RunNextAsync(default);

        Assert.Equal("failed", (await f.Status(job.Id)).State);
        var entry = Assert.Single(f.Logger.Entries);
        Assert.Same(error, entry.Error);
        Assert.Contains(job.Id.ToString(), entry.Message);
        Assert.Contains("chapter 1, section 1", entry.Message);
        Assert.Contains("synthesis", entry.Message);
    }

    private sealed class WorkerLogger : ILogger<AudiobookWorker>
    {
        public List<(string Message, Exception? Error)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((formatter(state, exception), exception));
    }

    private sealed class Fixture : IDisposable
    {
        public string Folder { get; } = Path.Combine(Path.GetTempPath(), "audiobook-jobs-" + Guid.NewGuid());
        public Guid NovelId { get; } = Guid.NewGuid();
        public FakeTts Tts { get; } = new();
        public TestClock Clock { get; } = new();
        public WorkerLogger Logger { get; } = new();
        public ServiceProvider Services { get; private set; } = null!;
        public AudiobookWorker Worker { get; private set; } = null!;
        public static async Task<Fixture> Create()
        {
            var f = new Fixture();
            Directory.CreateDirectory(f.Folder);
            f.Restart();
            using var scope = f.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            db.Novels.Add(new Novel { Id = f.NovelId, Title = "Book" });
            await db.SaveChangesAsync();
            await f.SetProse("First secret prose.", "Second secret prose.", "");
            return f;
        }
        public void Restart()
        {
            Worker?.Dispose();
            Services?.Dispose();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<TimeProvider>(Clock);
            services.Configure<AppStorageOptions>(o => o.DataFolder = Folder);
            services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={Folder}/app.db"));
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<INovelService, NovelService>();
            services.AddSingleton<IIntegrationsService, Integrations>();
            services.AddSingleton<ITtsVoiceRevisionService, NoRecordedVoices>();
            services.AddSingleton<ITextGenerationServiceResolver, NoText>();
            services.AddSingleton<ProviderSynthesisGate>();
            services.AddKeyedSingleton<ITtsService>(TtsProvider.ElevenLabs, (sp, _) => new GatedTtsService(Tts, TtsProvider.ElevenLabs, sp.GetRequiredService<ProviderSynthesisGate>()));
            services.AddSingleton<IAudioRepository, FileSystemWaveAudioRepository>();
            services.AddSingleton<IAudioArtifactRepository, FileSystemAudioArtifactRepository>();
            services.AddSingleton<AudioArtifactAssembler>();
            services.AddSingleton<AudiobookArchiveService>();
            services.AddSingleton<AudiobookPreparationCache>();
            services.AddScoped<IAudiobookTtsService, TtsAudioGenerationService>();
            services.AddSingleton<IAudiobookImmersivePlanner, NoPlan>();
            services.AddScoped<AudiobookSectionRenderer>();
            services.AddScoped<AudiobookSettingsResolver>();
            services.AddScoped<AudiobookSnapshotBuilder>();
            services.AddSingleton<AudiobookSnapshotStore>();
            services.AddSingleton<FileSystemAudiobookJobRepository>();
            services.AddSingleton<GeneratedAudioCleanup>();
            services.AddScoped<AudiobookJobService>();
            Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            Worker = new(Services.GetRequiredService<IServiceScopeFactory>(), Services.GetRequiredService<AudiobookSnapshotStore>(),
                Services.GetRequiredService<IOptions<AppStorageOptions>>(), Logger);
        }
        public async Task SetProse(params string[] texts)
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<INovelService>().UpdateProseAsync(NovelId,
                new Prose { Chapters = [new Chapter { Title = "Chapter", Sections = texts.Select(t => new Section { Text = t }).ToList() }, new Chapter { Title = "Empty", Sections = [] }] });
        }
        public async Task<T> Call<T>(Func<AudiobookJobService, Task<T>> call)
        {
            using var scope = Services.CreateScope();
            return await call(scope.ServiceProvider.GetRequiredService<AudiobookJobService>());
        }
        public Task<AudiobookPreview> Preview() => Call(s => s.PreviewAsync(NovelId, new(), default));
        public Task<AudiobookJobStatus> Start(AudiobookPreview preview, string key = "start", int parallelism = 1) => Call(s => s.StartAsync(NovelId, new(preview.SnapshotId, key, parallelism), default));
        public Task<AudiobookJobStatus> Status(Guid id) => Call(s => s.GetAsync(NovelId, id, default));
        public void Dispose() { Worker.Dispose(); Services.Dispose(); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(Folder, true); }
    }
    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class FakeTts : ITtsService
    {
        public List<string> Messages { get; } = [];
        private readonly object sync = new();
        private int activeCalls;
        public int MaximumActiveCalls { get; private set; }
        public Func<int, CancellationToken, Task>? BeforeCall;
        public AudioFormat OutputAudioFormat => AudioFormat.Wav;
        public async Task<byte[]> GenerateAudioAsync(TtsRequest request, CancellationToken cancellationToken = default)
        {
            int call;
            lock (sync)
            {
                Messages.Add(request.Message);
                call = Messages.Count;
                activeCalls++;
                MaximumActiveCalls = Math.Max(MaximumActiveCalls, activeCalls);
            }
            try
            {
                if (BeforeCall is not null) await BeforeCall(call, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                using var stream = new MemoryStream();
                using (var writer = new WaveFileWriter(stream, new WaveFormat(24000, 16, 1))) await writer.WriteAsync(new byte[480]);
                return stream.ToArray();
            }
            finally { lock (sync) activeCalls--; }
        }
        public Task<Stream> GenerateAudioStreamAsync(TtsRequest request, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream(new byte[10]));
        public Task<IEnumerable<TtsModelDto>> GetModelsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<TtsModelDto>>([new() { ModelId = "model", Name = "Model", Voices = [new() { VoiceId = "voice", Name = "Voice", Language = WritingLanguage.English }] }]);
        public Task<decimal?> GetBalanceUsdAsync(CancellationToken cancellationToken = default) => Task.FromResult<decimal?>(null);
    }
    private sealed class Integrations : IIntegrationsService
    {
        public ValueTask<IntegrationsConfig> GetConfigAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(new IntegrationsConfig
        {
            TtsProvider = TtsProvider.ElevenLabs, TtsModelId = "model", TtsVoiceId = "voice", TtsEnableTextEmphasis = false, TtsEnableImmersive = false
        });
        public Task UpdateConfigAsync(IntegrationsConfig config, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class NoText : ITextGenerationServiceResolver
    {
        public ValueTask<ITextGenerationService> GetConfiguredServiceAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class NoPlan : IAudiobookImmersivePlanner
    {
        public Task<ImmutableArray<AudiobookSpeechChunk>> PrepareAudiobookAsync(AudiobookRenderRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
