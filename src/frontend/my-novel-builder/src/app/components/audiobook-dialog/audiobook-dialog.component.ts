import { STREAMING_WAV_PLAYER_FACTORY, StreamingWavPlayerHandle } from '../../utils/streaming-wav-player.factory';
import { getVoicePreviewText } from '../../utils/tts-preview.utils';
import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { CheckboxModule } from 'primeng/checkbox';
import { DynamicDialogConfig } from 'primeng/dynamicdialog';
import { firstValueFrom, Observable, Subject, takeUntil } from 'rxjs';
import { v4 as uuid } from 'uuid';
import { AudiobookService } from '../../services/audiobook.service';
import { GenerateAudioService } from '../../services/generate-audio.service';
import { GenerateTextService } from '../../services/generate-text.service';
import { IntegrationsService } from '../../services/integrations.service';
import { PromptService } from '../../services/prompt.service';
import { AudiobookJob, AudiobookPreview, AudiobookSettings } from '../../types/dtos/generate/audiobook.dto';
import { TtsModelDto } from '../../types/dtos/generate/tts-model.dto';
import { TtsProviderDto } from '../../types/dtos/generate/tts-provider.dto';
import { TextGenerationModelInfoDto } from '../../types/dtos/generate/text-generation-model-info.dto';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { PromptType } from '../../types/enums/prompt-type';
import { TextGenerationProvider } from '../../types/enums/text-generation-provider';
import { SpacedPipe } from '../../pipes/spaced.pipe';

/** One export dialog. Inputs are local; closing stops observation, never the server job. */
@Component({
  selector: 'app-audiobook-dialog', standalone: true,
  imports: [CommonModule, FormsModule, ButtonModule, SelectModule, CheckboxModule, SpacedPipe],
  templateUrl: './audiobook-dialog.component.html', styleUrl: './audiobook-dialog.component.scss',
})
export class AudiobookDialogComponent implements OnInit, OnDestroy {
  readonly data = inject(DynamicDialogConfig).data as { novelId: string; updatedAt: string };
  readonly api = inject(AudiobookService);
  private readonly audio = inject(GenerateAudioService);
  private readonly text = inject(GenerateTextService);
  private readonly integrations = inject(IntegrationsService);
  private readonly promptsService = inject(PromptService);
  readonly textProviders = Object.values(TextGenerationProvider);
  settings: AudiobookSettings = {};
  defaults: AudiobookSettings = {};
  useDefaults = true;
  providers: TtsProviderDto[] = [];
  models: TtsModelDto[] = [];
  textModels: TextGenerationModelInfoDto[] = [];
  prompts: PromptDto[] = [];
  preview: AudiobookPreview | null = null;
  summary: AudiobookPreview | null = null;
  get previewExpired(): boolean { return !!this.preview && Date.parse(this.preview.expiresAt) <= Date.now(); }
  /** Narration estimate uses the whole book at 150 words/minute plus known section gaps. */
  get estimatedMinutes(): number {
    if (!this.summary) return 0;
    return Math.ceil(this.summary.spokenWords / 150 + Math.max(0, this.summary.eligibleSections - this.summary.eligibleChapters) / 120);
  }
  get overrideSettings(): boolean { return !this.useDefaults; }
  set overrideSettings(value: boolean) { this.useDefaults = !value; }
  previewingVoice = false;
  loadingVoice = false;
  private readonly createVoicePlayer = inject(STREAMING_WAV_PLAYER_FACTORY);
  private voicePlayer?: StreamingWavPlayerHandle;
  private voiceAbort?: AbortController;
  private voiceReader?: ReadableStreamDefaultReader<Uint8Array>;
  jobs: AudiobookJob[] = [];
  selectedJobId: string | null = null;
  loading = true;
  loadingJobs = true;
  loadingModels = false;
  loadingTextModels = false;
  busy = false;
  estimating = false;
  error: string | null = null;
  historyError: string | null = null;
  private submissionKey = '';
  private readonly destroyed = new Subject<void>();
  private closed = false;
  private estimateTimer?: ReturnType<typeof setTimeout>;
  private estimateRevision = 0;
  private initialized = false;
  private readonly estimateCancelled = new Subject<void>();
  private poll?: ReturnType<typeof setTimeout>;

  private modelRequest = 0;
  private textRequest = 0;
  private historyRequest = 0;

  ngOnInit(): void {
    this.initialized = true;
    void Promise.all([this.loadSettings(), this.refreshJobs()]).then(() => {
      if (!this.closed) void this.createPreview();
    });
  }

  /** Releases requests and timers without sending a cancellation request. */
  ngOnDestroy(): void {
    this.closed = true;
    clearTimeout(this.poll); clearTimeout(this.estimateTimer); this.stopVoicePreview();
    this.estimateCancelled.next(); this.estimateCancelled.complete();
    this.destroyed.next(); this.destroyed.complete();
  }

  private read<T>(source: Observable<T>): Promise<T> { return firstValueFrom(source.pipe(takeUntil(this.destroyed))); }

  /** Reloads defaults without writing them; an existing preview is discarded. */
  async loadSettings(): Promise<void> {
    this.loading = true; this.error = null; this.changed();
    try {
      const [config, providers, prompts] = await Promise.all([
        this.read(this.integrations.getIntegrationsConfig()), this.read(this.audio.getAvailableProviders()), this.read(this.promptsService.getPrompts()),
      ]);
      if (this.closed) return;
      this.providers = providers;
      this.prompts = prompts.filter(p => p.type === PromptType.PrepareImmersiveTts);
      this.defaults = { provider: config.ttsProvider, modelId: config.ttsModelId, voiceId: config.ttsVoiceId,
        enableTextEmphasis: config.ttsEnableTextEmphasis, enableImmersive: config.ttsEnableImmersive,
        immersivePauseMs: config.ttsImmersivePauseMs, textGenerationProvider: config.textGenerationProvider,
        textGenerationModelId: config.textGenerationModelId, immersivePromptId: this.prompts[0]?.id ?? null };
      this.settings = { ...this.defaults };
      await Promise.all([this.loadModels(false), this.loadTextModels(false)]);
    } catch (error) { this.showError(error); }
    finally { this.loading = false; this.scheduleEstimate(); }
  }

  get selectedModel(): TtsModelDto | undefined { return this.models.find(m => m.modelId === this.settings.modelId); }
  get voices() { return this.selectedModel?.voices ?? []; }
  get needsText(): boolean { return !!(this.settings.enableImmersive || this.settings.enableTextEmphasis); }
  get active(): boolean { return this.jobs.some(j => this.isActive(j)); }
  get selectedJob(): AudiobookJob | undefined { return this.jobs.find(j => j.id === this.selectedJobId); }
  get downloads(): AudiobookJob[] { return this.jobs.filter(j => j.downloadAvailable); }
  get valid(): boolean {
    return !this.loading && !this.loadingModels && !this.loadingTextModels && !!this.selectedModel &&
      this.voices.some(v => v.voiceId === this.settings.voiceId) &&
      (!this.settings.enableTextEmphasis || !!this.selectedModel.supportsTextEmphasis) &&
      (!this.needsText || this.textModels.some(m => m.id === this.settings.textGenerationModelId)) &&
      (!this.settings.enableImmersive || (this.prompts.some(p => p.id === this.settings.immersivePromptId) &&
        Number.isInteger(this.settings.immersivePauseMs) && this.settings.immersivePauseMs! >= 0 && this.settings.immersivePauseMs! <= 10000));
  }
  savedAfter(job: AudiobookJob): boolean { return Date.parse(this.data.updatedAt) > Date.parse(job.snapshotAt); }
  isActive(job: AudiobookJob): boolean { return ['queued', 'running', 'cancelling'].includes(job.state); }

  /** Changing any selection requires a fresh server preview before submission. */
  changed(): void {
    ++this.estimateRevision;
    this.estimateCancelled.next();
    this.estimating = false;
    this.preview = null; this.submissionKey = ''; this.stopVoicePreview();
    this.scheduleEstimate();
  }

  /** Coalesces edits; catalogue completion schedules another check if inputs were not ready yet. */
  private scheduleEstimate(): void {
    clearTimeout(this.estimateTimer);
    if (!this.initialized || this.closed) return;
    this.estimateTimer = setTimeout(() => void this.createPreview(), 400);
  }

  async modeChanged(): Promise<void> {
    this.changed();
    if (this.useDefaults) { await this.loadSettings(); }
  }

  /** Provider changes clear dependent selections; late catalogue responses cannot overwrite a newer choice. */
  async loadModels(clear = true): Promise<void> {
    this.changed();
    const request = ++this.modelRequest;
    if (clear) { this.settings.modelId = ''; this.settings.voiceId = ''; }
    this.models = []; this.loadingModels = true;
    try {
      const models = await this.read(this.audio.getAvailableModels(this.settings.provider ?? null));
      if (request !== this.modelRequest || this.closed) return;
      this.models = models;
    } catch (error) { if (request === this.modelRequest) this.showError(error); }
    finally { if (request === this.modelRequest) { this.loadingModels = false; this.scheduleEstimate(); } }
  }

  modelChanged(): void {
    this.changed(); this.settings.voiceId = '';
    if (!this.selectedModel?.supportsTextEmphasis) this.settings.enableTextEmphasis = false;
    void this.loadTextModels(false);
  }

  async loadTextModels(clear = true): Promise<void> {
    this.changed();
    const request = ++this.textRequest;
    if (clear) this.settings.textGenerationModelId = '';
    this.textModels = [];
    this.loadingTextModels = this.needsText;
    if (!this.needsText) return;
    try {
      const models = await this.read(this.text.getAvailableModelInfos(this.settings.textGenerationProvider));
      if (request === this.textRequest && !this.closed) this.textModels = models;
    } catch (error) { if (request === this.textRequest) this.showError(error); }
    finally { if (request === this.textRequest) { this.loadingTextModels = false; this.scheduleEstimate(); } }
  }

  /** Refreshes scope and reuse from saved prose using exactly the displayed narration settings. */
  async createPreview(): Promise<void> {
    clearTimeout(this.estimateTimer);
    if (this.closed || !this.valid || this.busy || this.active || this.loadingJobs || this.historyError) return;
    const revision = ++this.estimateRevision;
    this.estimateCancelled.next();
    this.estimating = true; this.error = null;
    this.preview = null; this.submissionKey = '';
    try {
      const preview = await this.read(this.api.preview(this.data.novelId, { ...this.settings }).pipe(takeUntil(this.estimateCancelled)));
      if (this.closed || revision !== this.estimateRevision) return;
      this.preview = this.summary = preview; this.submissionKey = uuid();
    } catch (error) { if (revision === this.estimateRevision) this.showError(error); }
    finally { if (revision === this.estimateRevision) this.estimating = false; }
  }

  /** Reuses the same submission key after a lost response, avoiding duplicate jobs. */
  async start(): Promise<void> {
    if (this.busy || this.estimating || this.active || this.historyError || this.loadingJobs || !this.valid) return;
    if (!this.preview || this.previewExpired) await this.createPreview();
    if (!this.preview || this.previewExpired || this.closed) return;
    this.stopVoicePreview();
    await this.run(this.api.start(this.data.novelId, this.preview.snapshotId, this.submissionKey));
  }

  /** Streams the same localized sample as Integrations, using the selected provider/model/voice. */
  async previewVoice(): Promise<void> {
    const voice = this.voices.find(v => v.voiceId === this.settings.voiceId);
    if (!voice || this.busy || this.loadingVoice) return;
    this.stopVoicePreview(); this.error = null;
    const abort = new AbortController();
    this.voiceAbort = abort;
    const player = this.createVoicePlayer();
    this.voicePlayer = player;
    this.loadingVoice = this.previewingVoice = true;
    try {
      const response = await this.audio.textToSpeechStreamResponse({
        message: getVoicePreviewText(voice.language), provider: this.settings.provider,
        modelId: this.settings.modelId, voiceId: voice.voiceId,
      }, abort.signal);
      if (abort.signal.aborted) { await response.body?.cancel(); return; }
      if (!response.body) throw new Error('No voice sample was returned. Check your speech provider settings.');
      const reader = response.body.getReader(); this.voiceReader = reader;
      while (!abort.signal.aborted) {
        const { done, value } = await reader.read();
        if (done) break;
        if (value && !abort.signal.aborted) player.addChunk(value);
      }
      reader.releaseLock();
    } catch (error) {
      if (!abort.signal.aborted) { this.stopVoicePreview(); this.showError(error); }
    } finally {
      if (this.voiceAbort === abort) { this.loadingVoice = false; this.voiceReader = undefined; }
    }
  }

  /** Stops playback and pending sample requests on selection changes or dialog close. */
  stopVoicePreview(): void {
    this.voiceAbort?.abort();
    void this.voiceReader?.cancel().catch(() => {});
    this.voicePlayer?.stop();
    this.voiceAbort = undefined; this.voiceReader = undefined; this.voicePlayer = undefined;
    this.loadingVoice = this.previewingVoice = false;
  }

  /** Opens the streamed archive directly in the browser without buffering it in JavaScript. */
  download(job: AudiobookJob): void {
    window.open(this.api.downloadUrl(this.data.novelId, job.id), '_blank', 'noopener');
  }

  async cancel(job: AudiobookJob): Promise<void> { if (!this.busy) await this.run(this.api.cancel(this.data.novelId, job.id)); }
  async retry(job: AudiobookJob): Promise<void> { if (!this.busy && !this.active) await this.run(this.api.retry(this.data.novelId, job.id)); }

  private async run(request: Observable<AudiobookJob>): Promise<void> {
    clearTimeout(this.estimateTimer);
    ++this.estimateRevision; this.estimateCancelled.next(); this.estimating = false;
    this.busy = true; this.error = null; clearTimeout(this.poll); ++this.historyRequest;
    try {
      const job = await this.read(request);
      if (this.closed) return;
      this.jobs = [job, ...this.jobs.filter(j => j.id !== job.id)]; this.selectedJobId = job.id;
      this.changed();
    } catch (error) { this.showError(error); }
    finally { this.busy = false; if (!this.closed) await this.refreshJobs(); }
  }

  /** Reconnects from durable status; polls only while a job is active and retries connection errors after five seconds. */
  async refreshJobs(): Promise<void> {
    clearTimeout(this.poll);
    const request = ++this.historyRequest;
    this.loadingJobs = true;
    try {
      const jobs = await this.read(this.api.list(this.data.novelId));
      if (this.closed || request !== this.historyRequest) return;
      const wasActive = this.active;
      const wasDisconnected = !!this.historyError;
      this.jobs = jobs; this.historyError = null;
      if ((wasActive || wasDisconnected) && !this.active && !this.preview) this.scheduleEstimate();
      if (!jobs.some(j => j.id === this.selectedJobId)) this.selectedJobId = jobs.find(j => this.isActive(j))?.id ?? jobs[0]?.id ?? null;
      if (this.active) this.poll = setTimeout(() => void this.refreshJobs(), 1500);
    } catch (error) {
      if (!this.closed && request === this.historyRequest) {
        this.historyError = this.message(error);
        this.poll = setTimeout(() => void this.refreshJobs(), 5000);
      }
    }
    finally { if (request === this.historyRequest) this.loadingJobs = false; }
  }

  stageLabel(stage: string): string {
    return ({ queued: 'Waiting to start', planning: 'Preparing narration', synthesis: 'Generating speech',
      rendering: 'Rendering sections', assembly: 'Assembling audio', packaging: 'Creating ZIP',
      ready: 'Download ready', sections_ready: 'Sections ready to package', interrupted: 'Interrupted',
      failed: 'Failed', cancelled: 'Cancelled' } as Record<string, string>)[stage] ?? stage;
  }

  jobError(job: AudiobookJob): string {
    const errors: Record<string, string> = {
      insufficient_disk_space: 'Free disk space, then retry. Completed audio will be reused.',
      wav_size_limit_split_chapter: 'A chapter exceeds the WAV size limit. Split it into smaller chapters and create a new preview.',
      interrupted_requires_verified_retry: 'The app stopped during generation. Retry checks that saved inputs still match.',
      snapshot_unavailable_requires_verified_retry: 'The snapshot is no longer in memory. Retry checks the saved inputs first.',
      provider_request_failed: 'The provider request failed. Check your provider settings and balance before retrying.',
      audio_storage_or_format_failed: 'Audio could not be read or saved. Check free disk space and retry.',
      generation_inputs_or_plan_invalid: 'Generation inputs or the immersive plan were invalid. Check settings and prompts, then retry or create a new preview.',
    };
    return errors[job.errorCode ?? ''] ?? 'Generation failed. Check your settings and retry; completed audio will be reused.';
  }

  private message(error: unknown): string {
    const response = error as { error?: { message?: string }; message?: string };
    return response?.error?.message || response?.message || 'Unable to reach the app. Refresh status and try again.';
  }
  private showError(error: unknown): void { if (!this.closed) this.error = this.message(error); }
}
