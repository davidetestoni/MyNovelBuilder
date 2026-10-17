import { STREAMING_WAV_PLAYER_FACTORY } from '../../utils/streaming-wav-player.factory';
import { TestBed, fakeAsync, flushMicrotasks, tick } from '@angular/core/testing';
import { DynamicDialogConfig } from 'primeng/dynamicdialog';
import { of, Subject, throwError } from 'rxjs';
import { AudiobookDialogComponent } from './audiobook-dialog.component';
import { AudiobookService } from '../../services/audiobook.service';
import { GenerateAudioService } from '../../services/generate-audio.service';
import { GenerateTextService } from '../../services/generate-text.service';
import { IntegrationsService } from '../../services/integrations.service';
import { PromptService } from '../../services/prompt.service';
import { AudiobookJob, AudiobookPreview } from '../../types/dtos/generate/audiobook.dto';
import { IntegrationsConfigDto } from '../../types/dtos/integrations/integrations-config.dto';
import { TtsModelDto } from '../../types/dtos/generate/tts-model.dto';
import { TtsProvider } from '../../types/enums/tts-provider';
import { TextGenerationProvider } from '../../types/enums/text-generation-provider';
import { WritingLanguage } from '../../types/enums/writing-language';

export const audiobookJob = (state: AudiobookJob['state'] = 'running'): AudiobookJob => ({
  id: 'job', novelId: 'novel', snapshotAt: new Date().toISOString(), state, stage: 'rendering', attemptNumber: 1,
  completedSections: 0, reusedSections: 0, generatedSections: 0, failedSections: 0, skippedSections: 0,
  completedChapters: 0, totalChapters: 1, downloadAvailable: state === 'completed', errorCode: null, sections: [], attempts: [],
});

describe('AudiobookDialogComponent', () => {
  let component: AudiobookDialogComponent;
  let player: jasmine.SpyObj<{ addChunk(chunk: Uint8Array): void; stop(): void }>;
  let api: jasmine.SpyObj<AudiobookService>;
  let audio: jasmine.SpyObj<GenerateAudioService>;
  let integrations: jasmine.SpyObj<IntegrationsService>;
  const models: TtsModelDto[] = [{ modelId: 'model', name: 'Model', supportsTextEmphasis: true,
    voices: [{ voiceId: 'voice', name: 'Voice', language: WritingLanguage.English, previewUrl: null }] }];
  const preview = (): AudiobookPreview => ({ snapshotId: 'snapshot', snapshotAt: new Date().toISOString(),
    expiresAt: new Date(Date.now() + 900000).toISOString(), settings: { provider: TtsProvider.ElevenLabs, modelId: 'model', voiceId: 'voice' },
    totalCharacters: 1200, spokenWords: 300, eligibleChapters: 1, eligibleSections: 2, reusableSections: 1, skippedSections: 1, skippedChapters: 1, charactersNeedingSynthesis: 100, warnings: [] });

  beforeEach(() => {
    player = jasmine.createSpyObj('player', ['addChunk', 'stop']);
    api = jasmine.createSpyObj('AudiobookService', ['preview', 'start', 'list', 'cancel', 'retry', 'downloadUrl']);
    audio = jasmine.createSpyObj('GenerateAudioService', ['getAvailableModels', 'getAvailableProviders', 'textToSpeechStreamResponse']);
    integrations = jasmine.createSpyObj('IntegrationsService', ['getIntegrationsConfig', 'updateIntegrationsConfig']);
    integrations.getIntegrationsConfig.and.returnValue(of({ ttsProvider: TtsProvider.ElevenLabs, ttsModelId: 'model', ttsVoiceId: 'voice',
      ttsEnableTextEmphasis: false, ttsEnableImmersive: false, ttsImmersivePauseMs: 150,
      textGenerationProvider: TextGenerationProvider.OpenRouter, textGenerationModelId: 'text' } as IntegrationsConfigDto));
    audio.getAvailableModels.and.returnValue(of(models));
    audio.getAvailableProviders.and.returnValue(of([{ provider: TtsProvider.ElevenLabs, supportsVoiceDesign: false }]));
    api.list.and.returnValue(of([])); api.preview.and.callFake(() => of(preview()));
    api.start.and.returnValue(of(audiobookJob())); api.cancel.and.returnValue(of(audiobookJob('cancelled')));
    api.retry.and.returnValue(of(audiobookJob())); api.downloadUrl.and.returnValue('/api/download');
    TestBed.configureTestingModule({ providers: [
      { provide: STREAMING_WAV_PLAYER_FACTORY, useValue: () => player },
      { provide: DynamicDialogConfig, useValue: { data: { novelId: 'novel', updatedAt: '2020-01-01' } } },
      { provide: AudiobookService, useValue: api }, { provide: GenerateAudioService, useValue: audio },
      { provide: IntegrationsService, useValue: integrations },
      { provide: GenerateTextService, useValue: { getAvailableModelInfos: () => of([{ id: 'text' }]) } },
      { provide: PromptService, useValue: { getPrompts: () => of([]) } },
    ] });
    component = TestBed.runInInjectionContext(() => new AudiobookDialogComponent());
  });
  afterEach(() => component.ngOnDestroy());

  it('previews defaults freely and does not update integrations', async () => {
    await component.loadSettings(); await component.refreshJobs();
    expect(component.valid).toBeTrue();
    await component.createPreview();
    expect(api.preview).toHaveBeenCalledOnceWith('novel', component.settings);
    expect(api.start).not.toHaveBeenCalled();
    expect(integrations.updateIntegrationsConfig).not.toHaveBeenCalled();
  });

  it('preserves explicit false and zero overrides and invalidates changed previews', async () => {
    await component.loadSettings(); await component.refreshJobs();
    component.useDefaults = false;
    component.settings.enableTextEmphasis = false; component.settings.enableImmersive = false; component.settings.immersivePauseMs = 0;
    await component.createPreview();
    expect(api.preview.calls.mostRecent().args[1]).toEqual(jasmine.objectContaining({ enableTextEmphasis: false, enableImmersive: false, immersivePauseMs: 0 }));
    component.changed(); expect(component.preview).toBeNull();
    expect(integrations.updateIntegrationsConfig).not.toHaveBeenCalled();
  });

  it('clears dependent selections and ignores a late provider catalogue', async () => {
    await component.loadSettings();
    const older = new Subject<TtsModelDto[]>(); const newer = new Subject<TtsModelDto[]>();
    audio.getAvailableModels.and.returnValues(older, newer);
    const a = component.loadModels(); const b = component.loadModels();
    expect(component.settings.modelId).toBe(''); expect(component.settings.voiceId).toBe('');
    newer.next([{ ...models[0], modelId: 'new' }]); await b;
    older.next(models); await a;
    expect(component.models[0].modelId).toBe('new');
  });

  it('reuses the submission key after a lost start response', async () => {
    await component.loadSettings(); await component.refreshJobs(); await component.createPreview();
    api.start.and.returnValues(throwError(() => new Error('Network error')), of(audiobookJob()));
    await component.start(); await component.start();
    expect(api.start.calls.count()).toBe(2);
    expect(api.start.calls.argsFor(0)).toEqual(api.start.calls.argsFor(1));
  });

  it('reconnects to active work and stops polling when closed without cancelling', fakeAsync(() => {
    api.list.and.returnValue(of([audiobookJob()]));
    void component.refreshJobs(); flushMicrotasks();
    expect(component.active).toBeTrue(); expect(component.selectedJobId).toBe('job');
    tick(1500); flushMicrotasks(); expect(api.list.calls.count()).toBe(2);
    component.ngOnDestroy(); tick(5000);
    expect(api.list.calls.count()).toBe(2); expect(api.cancel).not.toHaveBeenCalled();
  }));

  it('does not poll completed jobs and retains their download', fakeAsync(() => {
    api.list.and.returnValue(of([audiobookJob('completed')]));
    void component.refreshJobs(); flushMicrotasks(); tick(5000);
    expect(api.list.calls.count()).toBe(1); expect(component.downloads.length).toBe(1);
  }));

  it('automatically reconnects after polling errors', fakeAsync(() => {
    api.list.and.returnValue(throwError(() => new Error('offline')));
    void component.refreshJobs(); flushMicrotasks(); tick(5000);
    flushMicrotasks();
    expect(api.list.calls.count()).toBe(2); expect(component.historyError).toContain('offline');
    api.list.and.returnValue(of([])); tick(5000); flushMicrotasks();
    expect(component.historyError).toBeNull();
    component.ngOnDestroy();
  }));

  it('refreshes an expired snapshot automatically when generating', fakeAsync(() => {
    void component.loadSettings(); void component.refreshJobs(); flushMicrotasks();
    void component.createPreview(); flushMicrotasks(); tick(900001);
    expect(component.previewExpired).toBeTrue(); void component.start(); flushMicrotasks();
    expect(api.preview.calls.count()).toBe(2);
    expect(api.start.calls.count()).toBe(1);
  }));

  it('does not duplicate start while awaiting a response', async () => {
    await component.loadSettings(); await component.refreshJobs(); await component.createPreview();
    const pending = new Subject<AudiobookJob>(); api.start.and.returnValue(pending);
    const first = component.start(); await component.start();
    expect(api.start.calls.count()).toBe(1);
    pending.next(audiobookJob()); await first;
  });

  it('generates directly without a separate review step', async () => {
    await component.loadSettings(); await component.refreshJobs();
    await component.start();
    expect(api.preview).toHaveBeenCalledTimes(1);
    expect(api.start).toHaveBeenCalledTimes(1);
  });

  it('estimates the whole audiobook including reused sections and known gaps', async () => {
    await component.loadSettings(); await component.refreshJobs(); await component.createPreview();
    expect(component.estimatedMinutes).toBe(3); // 300 words / 150 + one 0.5 second gap.
    component.summary = { ...component.summary!, reusableSections: 2, charactersNeedingSynthesis: 0 };
    expect(component.estimatedMinutes).toBe(3);
    component.changed();
    expect(component.preview).toBeNull();
    expect(component.estimatedMinutes).toBe(3);
  });

  it('streams the selected voice sample and stops playback on setting changes', async () => {
    await component.loadSettings();
    const bytes = new Uint8Array([1, 2, 3]);
    audio.textToSpeechStreamResponse.and.resolveTo(new Response(new ReadableStream({ start(c) { c.enqueue(bytes); c.close(); } })));
    await component.previewVoice();
    const [request, signal] = audio.textToSpeechStreamResponse.calls.mostRecent().args;
    expect(request).toEqual(jasmine.objectContaining({ provider: TtsProvider.ElevenLabs, modelId: 'model', voiceId: 'voice' }));
    expect(player.addChunk).toHaveBeenCalledWith(bytes);
    component.changed();
    expect(player.stop).toHaveBeenCalled(); expect(signal?.aborted).toBeTrue();
  });

  it('aborts a pending sample when closed and never plays a late response', async () => {
    await component.loadSettings();
    let finish!: (response: Response) => void;
    audio.textToSpeechStreamResponse.and.returnValue(new Promise(resolve => finish = resolve));
    const playback = component.previewVoice();
    const signal = audio.textToSpeechStreamResponse.calls.mostRecent().args[1];
    component.ngOnDestroy();
    expect(signal?.aborted).toBeTrue();
    finish(new Response(new Uint8Array([1]))); await playback;
    expect(component.previewingVoice).toBeFalse();
  });

  it('automatically refreshes after edits settle without blocking settings', fakeAsync(() => {
    component.ngOnInit(); flushMicrotasks();
    expect(api.preview.calls.count()).toBe(1);
    component.settings.immersivePauseMs = 100; component.changed(); tick(200);
    component.settings.immersivePauseMs = 200; component.changed(); tick(399);
    expect(api.preview.calls.count()).toBe(1);
    tick(1); flushMicrotasks();
    expect(api.preview.calls.count()).toBe(2);
    expect(api.preview.calls.mostRecent().args[1].immersivePauseMs).toBe(200);
    expect(component.busy).toBeFalse();
    component.ngOnDestroy();
  }));

  it('discards a superseded estimate and cancels queued updates on close', fakeAsync(() => {
    component.ngOnInit(); flushMicrotasks();
    const older = new Subject<AudiobookPreview>();
    api.preview.and.returnValue(older);
    component.changed(); tick(400); flushMicrotasks();
    expect(component.estimating).toBeTrue();
    component.settings.immersivePauseMs = 0; component.changed();
    older.next({ ...preview(), reusableSections: 99 }); flushMicrotasks();
    expect(component.summary?.reusableSections).not.toBe(99);
    api.preview.and.callFake(() => of(preview()));
    tick(400); flushMicrotasks();
    expect(component.preview).not.toBeNull();
    component.changed(); component.ngOnDestroy();
    const calls = api.preview.calls.count(); tick(1000);
    expect(api.preview.calls.count()).toBe(calls);
  }));

  it('waits for valid catalogue selections before estimating', fakeAsync(() => {
    component.ngOnInit(); flushMicrotasks();
    component.settings.modelId = ''; component.changed(); tick(400); flushMicrotasks();
    expect(api.preview.calls.count()).toBe(1);
    component.settings.modelId = 'model'; component.changed(); tick(400); flushMicrotasks();
    expect(api.preview.calls.count()).toBe(2);
    component.ngOnDestroy();
  }));

  it('discards pending estimates when retrying an existing job', async () => {
    await component.loadSettings(); await component.refreshJobs();
    const pending = new Subject<AudiobookPreview>(); api.preview.and.returnValue(pending);
    const estimate = component.createPreview();
    await component.retry(audiobookJob('failed'));
    pending.next(preview()); await estimate;
    expect(component.preview).toBeNull();
    expect(component.estimating).toBeFalse();
  });

  it('renders labelled controls and downloads through the standard button component', async () => {
    component.ngOnDestroy();
    api.list.and.returnValue(of([audiobookJob('completed')]));
    const fixture = TestBed.createComponent(AudiobookDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    expect(element.querySelector('label[for="ab-provider"]')?.textContent).toContain('Speech provider');
    const open = spyOn(window, 'open');
    const download = element.querySelector<HTMLButtonElement>('.history-column p-button.download button');
    expect(download).not.toBeNull();
    download!.click();
    expect(open).toHaveBeenCalledWith('/api/download', '_blank', 'noopener');
    expect(element.querySelector('.summary-column .history')).toBeNull();
    expect(element.textContent).toContain('last saved prose');
    expect(element.querySelector('input[type=radio]')).toBeNull();
    expect(element.textContent).toContain('Override default settings');
    expect(element.textContent).toContain('Preview voice');
    expect(element.textContent).not.toContain('Review before generating');
    expect(element.textContent).not.toContain('charges');
    expect(element.textContent).not.toContain('Download format');
    expect(element.textContent).not.toContain('Update estimate');
    expect(element.textContent).not.toContain('Reload defaults');
    expect(element.querySelector('.estimate-note')).not.toBeNull();
    fixture.destroy();
  });
});
