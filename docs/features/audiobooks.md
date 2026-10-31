# Generate an audiobook

Open **Novel Settings → Generate audiobook** to turn a novel’s saved prose
into downloadable chapter audio. Configure a speech provider and narrator
voice in **Integrations** first. Local TTS servers and external providers use
the same workflow.

## Choose narration and generate

The dialog starts with your default speech settings. Tick **Override default
settings** to choose a provider, model, narrator voice or supported options
for this export. Overrides do not change Integrations. Untick the checkbox to
reload the defaults. **Preview voice** plays a short sample of the selected
voice.

Choose narrator-only or immersive narration. Immersive mode needs a
**Prepare immersive TTS** prompt and a text-generation provider/model. It uses
character voice assignments where available; unassigned characters use the
narrator. Text emphasis is available only for models that support it.

Annotations guide delivery in both narration and dialogue. Supported profiles
include ElevenLabs v3, ElevenLabs v3 through NanoGPT, OmniVoice, and Fish Audio
Drama 3 through OpenRouter. Emphasis is off by default. When enabled, it applies to narration and dialogue;
existing explicit preferences are preserved. Immersive mode annotates the section
before assigning speakers, then preserves those directions through synthesis
chunking. Annotation preparation uses the selected text-generation model and
does not change saved prose.
If annotation output still fails text-preservation checks after a retry, speech
uses the original passage without added annotations.

Scope and reuse estimates update automatically as you change settings. They
show spoken chapters/sections, skipped empty content, reusable audio, and
characters needing generation. The rough audio estimate uses 150 words per
minute plus the gaps between sections; voice pacing and immersive pauses can
change the actual duration.

Click **Generate audiobook**. Generation uses the last saved prose and freezes
its inputs when the snapshot is created. Later edits do not change that run.
Each provider generates one operation at a time, shared with playback and
voice previews. External providers can charge for speech and text preparation.

## Progress, cancellation and retry

**Saved runs** shows progress and existing downloads. Closing the dialog or
navigating away leaves the job running while the app remains open. Reopening
the dialog reconnects to it. Connection errors retry automatically.

**Cancel generation** stops queued work and later audiobook chunks. The
current provider operation finishes before its slot becomes available, so
cancellation can take time. Completed cached audio remains available for retry.

Use **Retry** after failure or cancellation. While the app remains open, retry
uses the original in-memory snapshot, even if you edited the novel. After
restarting the app, retry reconstructs the saved inputs and checks their
hashes. If prose, prompts, settings or voice references changed, create a new
run. Incomplete emphasis or immersive planning may need another provider call
after restart. The app never automatically resumes paid generation on startup.

Repeating unchanged generation reuses complete audio. Narrator edits normally
regenerate only affected sections. Immersive planning uses story context, so
an earlier edit may also affect later sections. Missing or damaged files are
rebuilt from the nearest retained audio layer.

### Diagnosing a failed run

The failed run shows a **Job ID**. Search the backend console logs for that ID.
For Docker, run:

```sh
docker compose logs --follow app
```

Failure logs include the exception and stack trace, chapter and section numbers
(starting at 1), stage, TTS provider and model. HTTP failures inside generation
also include the status, provider host, `X-Request-Id` when supplied, and up to
8,192 characters of the provider's error response. These error responses appear
at Warning/Error level; Debug logging is not required. Provider responses can
contain provider-supplied details, so review logs before sharing them.

Each section has at most three automatic attempts for connection failures,
timeouts, and HTTP 408, 429, 500, 502, 503 or 504 responses. Retries wait 1 then
2 seconds, or use the provider's `Retry-After` header when supplied. A hint over
30 seconds stops automatic retries; use **Retry** after the cooldown. Each retry
logs its failed attempt and delay. Authentication, payment and invalid-request
errors are not automatically retried. Completed cached audio is reused when a
section is retried, though failed or incomplete provider operations may be charged
again. Other SDK exceptions without a recognizable HTTP status are logged but
are not automatically retried.

## Download

A completed run offers **Download ZIP**. It contains one WAV file for each
non-empty chapter and a `manifest.json` with chapter titles, duration,
checksums and section offsets. Chapter filenames retain their original order;
empty chapters and sections are skipped. Titles name files but are not spoken.

WAV chapters use 24 kHz mono 16-bit PCM with a 500 ms gap between sections and
no trailing gap. Higher-rate provider audio is downsampled with a sinc low-pass
filter to suppress aliasing; matching PCM is copied without requantization.
After upgrading from the earlier assembly filter, create a new run to use the
improved conversion. Existing downloads and completed section checkpoints in
old runs keep their original audio. New runs reuse cached provider synthesis
where its inputs match, but may need new narration planning.
Expect about 2.9 MB per audio minute (173 MB per hour), plus
small file/ZIP headers. WAV entries are not compressed, so book-sized exports
can be large. A chapter exceeding the WAV container limit must be split into
smaller chapters before generating again.

A later failed run leaves earlier downloads available. An expired or missing
ZIP shows **Rebuild download**. Rebuilding uses retained audio and the same
input-verification rules as retry. If the saved novel changed after an app
restart, generate a new run instead. Save a copy of any ZIP you want to keep
independently of app cleanup.

## Storage and cleanup

Generated files are private files under the application’s data directory:

- `audio/v1/`: shared speech, section and chapter WAVs, integrity information,
  and hash-only dependency maps.
- `audiobooks/<novel-id>/jobs/`: small JSON records for saved runs.
- `audiobooks/<novel-id>/archives/`: downloadable ZIPs.

The [development guide](../development.md#development-data) lists the data
locations and overrides; Docker keeps them in its persistent data volume.
Job/cache metadata contains hashes and progress, not prose, rendered prompts
or credentials. Generated audio naturally contains the spoken story, and ZIP
manifests include the novel/chapter titles and author. Keep backups of the
application-data directory and downloaded books as needed.

Cleanup runs when the application starts, before accepting requests:

- Abandoned temporary files older than 24 hours are removed.
- Unused ZIPs and unreferenced cache files older than 90 days are removed.
- The latest successful download for each novel stays available. If its newest
  ZIP is missing, the most recent surviving successful download is preserved.
- Saved run history and section audio referenced by it are retained. Audio
  dependency maps protect underlying chunks and retained chapter assemblies.
- Successful downloads and cache reuse refresh file timestamps, at most once
  per day. Recently reused files therefore stay beyond their creation date.
- Unreadable job metadata prevents permanent-file cleanup. Unreadable dependency
  metadata prevents shared-cache cleanup. Locked/unwritable files are skipped.

Cleanup uses age and references rather than a hard disk quota. It does not run
periodically during a session; restart the app to run it again. It does not
remove legacy playback caches, novels, voice samples, credentials or media.
Deleting a novel removes its private jobs/ZIPs; shared audio becomes eligible
for cleanup when it is old and no longer referenced.

Set `GeneratedAudioRetentionDays` in the backend’s `appsettings.json`, or the
same-named environment variable, to change the 90-day age limit. For example:

```json
{
  "GeneratedAudioRetentionDays": 30
}
```

Set it to `0` to disable age-based removal of ZIPs and cache files. Temporary
file cleanup still runs. Existing saved jobs need no database migration.
