import { TestBed } from '@angular/core/testing';
import { ToastrService } from 'ngx-toastr';
import { Subject } from 'rxjs';
import { GenerateAudioService } from '../../services/generate-audio.service';
import { IntegrationsService } from '../../services/integrations.service';
import { LocalStorageService } from '../../services/local-storage.service';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { PromptType } from '../../types/enums/prompt-type';
import {
  STREAMING_WAV_PLAYER_FACTORY,
  StreamingWavPlayerFactory,
  StreamingWavPlayerHandle,
} from '../../utils/streaming-wav-player.factory';
import { ProseTtsRequest, ProseTtsService } from './prose-tts.service';

describe('ProseTtsService', () => {
  let service: ProseTtsService;
  let generateAudioService: jasmine.SpyObj<GenerateAudioService>;
  let localStorageService: jasmine.SpyObj<LocalStorageService>;
  let toastr: jasmine.SpyObj<ToastrService>;
  let integrationsConfig$: Subject<{ ttsEnableImmersive: boolean }>;
  let player: jasmine.SpyObj<StreamingWavPlayerHandle>;
  let createPlayer: jasmine.Spy<StreamingWavPlayerFactory>;
  let firstAudioCallback: (() => void) | undefined;

  const playableResponse = () => new Response(new Uint8Array(45));

  const createPrompt = (id: string): PromptDto => ({
    id,
    createdAt: '',
    updatedAt: '',
    name: id,
    type: PromptType.PrepareImmersiveTts,
    messages: [],
  });

  const createRequest = (prompts: PromptDto[] = []): ProseTtsRequest => ({
    novelId: 'novel-1',
    prompts,
    chapterIndex: 2,
    sectionIndex: 3,
    narratorText: 'Narrator text',
  });

  beforeEach(() => {
    generateAudioService = jasmine.createSpyObj<GenerateAudioService>(
      'GenerateAudioService',
      ['immersiveTextToSpeechStreamResponse', 'textToSpeechStreamResponse'],
    );
    localStorageService = jasmine.createSpyObj<LocalStorageService>(
      'LocalStorageService',
      [
        'getNestedStringForKey',
        'removeNestedKey',
        'setNestedStringForKey',
      ],
    );
    toastr = jasmine.createSpyObj<ToastrService>('ToastrService', [
      'clear',
      'error',
      'info',
      'warning',
    ]);
    toastr.info.and.returnValue({ toastId: 41 } as never);
    integrationsConfig$ = new Subject<{ ttsEnableImmersive: boolean }>();
    player = jasmine.createSpyObj<StreamingWavPlayerHandle>(
      'StreamingWavPlayerHandle',
      ['addChunk', 'stop'],
    );
    createPlayer = jasmine.createSpy<StreamingWavPlayerFactory>(
      'StreamingWavPlayerFactory',
    ).and.callFake((callback) => {
      firstAudioCallback = callback;
      return player;
    });

    TestBed.configureTestingModule({
      providers: [
        { provide: GenerateAudioService, useValue: generateAudioService },
        { provide: LocalStorageService, useValue: localStorageService },
        { provide: ToastrService, useValue: toastr },
        {
          provide: IntegrationsService,
          useValue: { getIntegrationsConfig: () => integrationsConfig$ },
        },
        { provide: STREAMING_WAV_PLAYER_FACTORY, useValue: createPlayer },
      ],
    });

    spyOn(console, 'error');
    spyOn(console, 'time');
    spyOn(console, 'timeEnd');

    service = TestBed.runInInjectionContext(() => new ProseTtsService());
    integrationsConfig$.next({ ttsEnableImmersive: false });
  });

  it('streams narrator audio and clears loading on first audio', async () => {
    generateAudioService.textToSpeechStreamResponse.and.resolveTo(
      playableResponse(),
    );
    player.addChunk.and.callFake(() => firstAudioCallback?.());

    await service.playSection(createRequest());

    expect(
      generateAudioService.textToSpeechStreamResponse,
    ).toHaveBeenCalledOnceWith({ message: 'Narrator text' });
    expect(createPlayer).toHaveBeenCalledTimes(1);
    expect(player.addChunk).toHaveBeenCalled();
    expect(toastr.info).toHaveBeenCalledWith(
      'Generating TTS...',
      '',
      jasmine.objectContaining({ timeOut: 0, tapToDismiss: false }),
    );
    expect(toastr.clear).toHaveBeenCalledOnceWith(41);
  });

  it('uses the stored immersive prompt when it remains available', async () => {
    integrationsConfig$.next({ ttsEnableImmersive: true });
    localStorageService.getNestedStringForKey.and.returnValue('immersive-1');
    generateAudioService.immersiveTextToSpeechStreamResponse.and.resolveTo(
      playableResponse(),
    );

    await service.playSection(createRequest([createPrompt('immersive-1')]));

    expect(
      generateAudioService.immersiveTextToSpeechStreamResponse,
    ).toHaveBeenCalledOnceWith({
      novelId: 'novel-1',
      promptId: 'immersive-1',
      chapterIndex: 2,
      sectionIndex: 3,
    });
    expect(
      generateAudioService.textToSpeechStreamResponse,
    ).not.toHaveBeenCalled();
  });

  it('replaces a stale immersive preference with the first available prompt', async () => {
    integrationsConfig$.next({ ttsEnableImmersive: true });
    localStorageService.getNestedStringForKey.and.returnValue('removed');
    generateAudioService.immersiveTextToSpeechStreamResponse.and.resolveTo(
      playableResponse(),
    );

    await service.playSection(createRequest([createPrompt('fallback')]));

    expect(localStorageService.removeNestedKey).toHaveBeenCalledOnceWith(
      LocalStorageKey.RecentPrompts,
      PromptType.PrepareImmersiveTts,
    );
    expect(
      localStorageService.setNestedStringForKey,
    ).toHaveBeenCalledOnceWith(
      LocalStorageKey.RecentPrompts,
      PromptType.PrepareImmersiveTts,
      'fallback',
    );
  });

  it('falls back to narrator audio when immersive playback fails', async () => {
    integrationsConfig$.next({ ttsEnableImmersive: true });
    generateAudioService.immersiveTextToSpeechStreamResponse.and.rejectWith(
      new Error('provider unavailable'),
    );
    generateAudioService.textToSpeechStreamResponse.and.resolveTo(
      playableResponse(),
    );

    await service.playSection(createRequest([createPrompt('immersive')]));

    expect(toastr.warning).toHaveBeenCalledOnceWith(
      'Immersive TTS failed (provider unavailable). Falling back to narrator-only playback.',
    );
    expect(generateAudioService.textToSpeechStreamResponse).toHaveBeenCalled();
  });

  it('explains the narrator fallback when no immersive prompt is configured', async () => {
    integrationsConfig$.next({ ttsEnableImmersive: true });
    generateAudioService.textToSpeechStreamResponse.and.resolveTo(
      playableResponse(),
    );

    await service.playSection(createRequest());

    expect(toastr.info).toHaveBeenCalledWith(
      'No immersive TTS prompt is configured. Falling back to narrator-only playback.',
    );
  });

  it('reports a missing response body and rejects header-only audio', async () => {
    generateAudioService.textToSpeechStreamResponse.and.resolveTo(
      new Response(null),
    );
    await service.playSection(createRequest());
    expect(toastr.error).toHaveBeenCalledOnceWith(
      'No audio stream was returned.',
    );

    toastr.error.calls.reset();
    generateAudioService.textToSpeechStreamResponse.and.resolveTo(
      new Response(new Uint8Array(44)),
    );
    await service.playSection(createRequest());

    expect(console.error).toHaveBeenCalledWith(
      'WAV streaming error:',
      jasmine.any(Error),
    );
    expect(toastr.clear).toHaveBeenCalledWith(41);
  });

  it('falls back safely when integrations configuration cannot be loaded', async () => {
    integrationsConfig$.error(new Error('config failed'));
    generateAudioService.textToSpeechStreamResponse.and.resolveTo(
      playableResponse(),
    );

    await service.playSection(createRequest([createPrompt('immersive')]));

    expect(console.error).toHaveBeenCalledWith(
      'Error loading integrations config for TTS:',
      jasmine.any(Error),
    );
    expect(
      generateAudioService.immersiveTextToSpeechStreamResponse,
    ).not.toHaveBeenCalled();
    expect(generateAudioService.textToSpeechStreamResponse).toHaveBeenCalled();
  });

  it('clears an outstanding loading toast when destroyed', async () => {
    let resolveResponse!: (response: Response) => void;
    generateAudioService.textToSpeechStreamResponse.and.returnValue(
      new Promise<Response>((resolve) => {
        resolveResponse = resolve;
      }),
    );

    const playback = service.playSection(createRequest());
    await Promise.resolve();
    service.ngOnDestroy();

    expect(toastr.clear).toHaveBeenCalledOnceWith(41);

    resolveResponse(playableResponse());
    await playback;
  });
});
