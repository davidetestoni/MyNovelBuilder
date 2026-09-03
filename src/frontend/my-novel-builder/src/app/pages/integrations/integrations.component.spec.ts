import { TestBed } from '@angular/core/testing';
import { ToastrService } from 'ngx-toastr';
import { of, Subject, throwError } from 'rxjs';
import { GenerateAudioService } from '../../services/generate-audio.service';
import { GenerateTextService } from '../../services/generate-text.service';
import { IntegrationsService } from '../../services/integrations.service';
import { TextGenerationModelInfoDto } from '../../types/dtos/generate/text-generation-model-info.dto';
import { TtsModelDto } from '../../types/dtos/generate/tts-model.dto';
import { IntegrationsConfigDto } from '../../types/dtos/integrations/integrations-config.dto';
import { ImageGenerationProvider } from '../../types/enums/image-generation-provider';
import { TextGenerationProvider } from '../../types/enums/text-generation-provider';
import { TtsProvider } from '../../types/enums/tts-provider';
import { VideoGenerationProvider } from '../../types/enums/video-generation-provider';
import { WritingLanguage } from '../../types/enums/writing-language';
import {
  STREAMING_WAV_PLAYER_FACTORY,
  StreamingWavPlayerFactory,
  StreamingWavPlayerHandle,
} from '../../utils/streaming-wav-player.factory';
import { IntegrationsComponent } from './integrations.component';

describe('IntegrationsComponent workflows', () => {
  let component: IntegrationsComponent;
  let integrationsService: jasmine.SpyObj<IntegrationsService>;
  let generateAudioService: jasmine.SpyObj<GenerateAudioService>;
  let generateTextService: jasmine.SpyObj<GenerateTextService>;
  let toastr: jasmine.SpyObj<ToastrService>;
  let player: jasmine.SpyObj<StreamingWavPlayerHandle>;
  let createPlayer: jasmine.Spy<StreamingWavPlayerFactory>;

  const config = (
    overrides: Partial<IntegrationsConfigDto> = {},
  ): IntegrationsConfigDto => ({
    hasOpenRouterApiKey: true,
    hasGoogleGenAiApiKey: false,
    hasElevenLabsApiKey: true,
    hasUnrealSpeechApiKey: false,
    hasDeApiApiKey: true,
    hasNanoGptApiKey: true,
    customTtsBaseUrl: 'http://custom/',
    pocketTtsBaseUrl: 'http://pocket/',
    vibeVoiceBaseUrl: 'http://vibe/',
    chatterboxBaseUrl: 'http://chatterbox/',
    qwen3BaseUrl: 'http://qwen/',
    omniVoiceBaseUrl: 'http://omni/',
    audio8BaseUrl: 'http://audio8/',
    textGenerationProvider: TextGenerationProvider.OpenRouter,
    textGenerationModelId: 'structured-model',
    ttsProvider: TtsProvider.Qwen3,
    imageGenerationProvider: ImageGenerationProvider.OpenRouter,
    videoGenerationProvider: VideoGenerationProvider.DeApi,
    ttsModelId: 'expressive',
    ttsVoiceId: 'italian-voice',
    ttsEnableTextEmphasis: true,
    ttsEnableImmersive: true,
    ttsImmersivePauseMs: 275,
    ...overrides,
  });

  const ttsModels = (): TtsModelDto[] => [
    {
      modelId: 'basic',
      name: 'Basic model',
      supportsTextEmphasis: false,
      voices: [
        {
          voiceId: 'english-voice',
          name: 'English voice',
          previewUrl: null,
          language: WritingLanguage.English,
        },
      ],
    },
    {
      modelId: 'expressive',
      name: 'Expressive model',
      supportsTextEmphasis: true,
      voices: [
        {
          voiceId: 'italian-voice',
          name: 'Italian voice',
          previewUrl: null,
          language: WritingLanguage.Italian,
        },
        {
          voiceId: 'spanish-voice',
          name: 'Spanish voice',
          previewUrl: null,
          language: WritingLanguage.Spanish,
        },
      ],
    },
  ];

  const modelInfo = (
    id: string,
    supportsStructuredOutputs: boolean,
  ): TextGenerationModelInfoDto => ({
    id,
    supportsStructuredOutputs,
    isVisionCapable: false,
    inputTokenPrice: 0,
    outputTokenPrice: 0,
  });

  const responseWithChunks = (...chunks: Uint8Array[]): Response => {
    const reads: ReadableStreamReadResult<Uint8Array>[] = [
      ...chunks.map((value) => ({ done: false as const, value })),
      { done: true as const, value: undefined },
    ];

    return {
      body: {
        getReader: () => ({
          read: async () => reads.shift()!,
        }),
      },
    } as unknown as Response;
  };

  beforeEach(() => {
    integrationsService = jasmine.createSpyObj<IntegrationsService>(
      'IntegrationsService',
      ['getIntegrationsConfig', 'updateIntegrationsConfig'],
    );
    generateAudioService = jasmine.createSpyObj<GenerateAudioService>(
      'GenerateAudioService',
      ['getAvailableModels', 'getBalanceUsd', 'textToSpeechStreamResponse'],
    );
    generateTextService = jasmine.createSpyObj<GenerateTextService>(
      'GenerateTextService',
      ['getAvailableModelInfos', 'sortModels'],
    );
    toastr = jasmine.createSpyObj<ToastrService>('ToastrService', [
      'success',
      'error',
    ]);
    player = jasmine.createSpyObj<StreamingWavPlayerHandle>(
      'StreamingWavPlayerHandle',
      ['addChunk', 'stop'],
    );
    createPlayer = jasmine.createSpy<StreamingWavPlayerFactory>(
      'StreamingWavPlayerFactory',
    );

    integrationsService.getIntegrationsConfig.and.returnValue(of(config()));
    integrationsService.updateIntegrationsConfig.and.returnValue(of(undefined));
    generateAudioService.getAvailableModels.and.returnValue(of(ttsModels()));
    generateAudioService.getBalanceUsd.and.returnValue(of(12.34));
    generateTextService.getAvailableModelInfos.and.returnValue(
      of([
        modelInfo('unstructured-model', false),
        modelInfo('structured-model', true),
      ]),
    );
    generateTextService.sortModels.and.callFake((models) => [...models].sort());
    createPlayer.and.returnValue(player);

    TestBed.configureTestingModule({
      providers: [
        { provide: IntegrationsService, useValue: integrationsService },
        { provide: GenerateAudioService, useValue: generateAudioService },
        { provide: GenerateTextService, useValue: generateTextService },
        { provide: ToastrService, useValue: toastr },
        { provide: STREAMING_WAV_PLAYER_FACTORY, useValue: createPlayer },
      ],
    });

    component = TestBed.runInInjectionContext(
      () => new IntegrationsComponent(),
    );
  });

  it('loads configuration, models, API-key status, and configured balances', () => {
    component.ngOnInit();

    expect(component.hasOpenRouterApiKey).toBeTrue();
    expect(component.hasGoogleGenAiApiKey).toBeFalse();
    expect(component.hasElevenLabsApiKey).toBeTrue();
    expect(component.hasUnrealSpeechApiKey).toBeFalse();
    expect(component.hasDeApiApiKey).toBeTrue();
    expect(component.hasNanoGptApiKey).toBeTrue();
    expect(component.integrationsForm.value).toEqual(
      jasmine.objectContaining({
        textGenerationProvider: TextGenerationProvider.OpenRouter,
        textGenerationModelId: 'structured-model',
        ttsProvider: TtsProvider.Qwen3,
        ttsModelId: 'expressive',
        ttsVoiceId: 'italian-voice',
        customTtsBaseUrl: 'http://custom/',
        ttsEnableTextEmphasis: true,
        ttsEnableImmersive: true,
        ttsImmersivePauseMs: 275,
        imageGenerationProvider: ImageGenerationProvider.OpenRouter,
      }),
    );
    expect(generateTextService.getAvailableModelInfos).toHaveBeenCalledOnceWith(
      TextGenerationProvider.OpenRouter,
    );
    expect(generateAudioService.getAvailableModels).toHaveBeenCalledOnceWith(
      TtsProvider.Qwen3,
    );
    expect(generateAudioService.getBalanceUsd.calls.allArgs()).toEqual([
      [TtsProvider.DeApi],
      [TtsProvider.NanoGpt],
    ]);
  });

  it('logs configuration loading errors without starting dependent requests', () => {
    const error = new Error('configuration failed');
    const consoleError = spyOn(console, 'error');
    integrationsService.getIntegrationsConfig.and.returnValue(
      throwError(() => error),
    );

    component.ngOnInit();

    expect(consoleError).toHaveBeenCalledOnceWith(
      'Error loading configuration:',
      error,
    );
    expect(generateAudioService.getAvailableModels).not.toHaveBeenCalled();
    expect(generateTextService.getAvailableModelInfos).not.toHaveBeenCalled();
  });

  it('reloads model choices when either provider changes', () => {
    component.ngOnInit();
    generateAudioService.getAvailableModels.calls.reset();
    generateTextService.getAvailableModelInfos.calls.reset();
    component.hasGoogleGenAiApiKey = true;
    component.integrationsForm.controls.textGenerationProvider.setValue(
      TextGenerationProvider.GoogleGenAi,
    );
    component.integrationsForm.controls.ttsProvider.setValue(
      TtsProvider.PocketTts,
    );

    expect(generateTextService.getAvailableModelInfos).toHaveBeenCalledOnceWith(
      TextGenerationProvider.GoogleGenAi,
    );
    expect(generateAudioService.getAvailableModels).toHaveBeenCalledOnceWith(
      TtsProvider.PocketTts,
    );
  });

  it('does not request model lists before the selected provider key is configured', () => {
    integrationsService.getIntegrationsConfig.and.returnValue(
      of(config({
        hasOpenRouterApiKey: false,
        hasElevenLabsApiKey: false,
        textGenerationProvider: TextGenerationProvider.OpenRouter,
        ttsProvider: TtsProvider.ElevenLabs,
      })),
    );

    component.ngOnInit();

    expect(generateTextService.getAvailableModelInfos).not.toHaveBeenCalled();
    expect(generateAudioService.getAvailableModels).not.toHaveBeenCalled();
    expect(component['textGenerationModelsUnavailableMessage']).toBe(
      'Save an OpenRouter API key to load available models.',
    );
    expect(component['ttsModelsUnavailableMessage']).toBe(
      'Save an ElevenLabs API key to load available models and voices.',
    );
    expect(component.integrationsForm.value.textGenerationModelId).toBe('');
    expect(component.integrationsForm.value.ttsModelId).toBe('');
    expect(component.integrationsForm.value.ttsVoiceId).toBe('');
  });

  it('filters out text models without structured-output support and sorts options', () => {
    generateTextService.getAvailableModelInfos.and.returnValue(
      of([
        modelInfo('zeta', true),
        modelInfo('ignored', false),
        modelInfo('alpha', true),
      ]),
    );
    integrationsService.getIntegrationsConfig.and.returnValue(
      of(config({ textGenerationModelId: 'zeta' })),
    );

    component.ngOnInit();

    expect(component.availableTextGenerationModels.map((model) => model.id)).toEqual([
      'zeta',
      'alpha',
    ]);
    expect(component['textGenerationModelOptions']).toEqual([
      { label: 'alpha', value: 'alpha' },
      { label: 'zeta', value: 'zeta' },
    ]);
    expect(generateTextService.sortModels).toHaveBeenCalledWith([
      'zeta',
      'alpha',
    ]);
  });

  it('clears an invalid configured text model', () => {
    integrationsService.getIntegrationsConfig.and.returnValue(
      of(config({ textGenerationModelId: 'missing' })),
    );

    component.ngOnInit();

    expect(component.integrationsForm.value.textGenerationModelId).toBe('');
  });

  it('clears text models and selection when loading them fails', () => {
    const error = new Error('models failed');
    const consoleError = spyOn(console, 'error');
    generateTextService.getAvailableModelInfos.and.returnValue(
      throwError(() => error),
    );

    component.ngOnInit();

    expect(consoleError).toHaveBeenCalledOnceWith(
      'Error loading text generation models:',
      error,
    );
    expect(component.availableTextGenerationModels).toEqual([]);
    expect(component.integrationsForm.value.textGenerationModelId).toBe('');
  });

  it('uses a configured TTS model and voice when both are valid', () => {
    component.ngOnInit();

    expect(component.integrationsForm.value.ttsModelId).toBe('expressive');
    expect(component.integrationsForm.value.ttsVoiceId).toBe('italian-voice');
    expect(component['ttsModelOptions']).toEqual([
      { label: 'Basic model', value: 'basic' },
      { label: 'Expressive model', value: 'expressive' },
    ]);
    expect(component['ttsVoiceOptions']).toEqual([
      {
        label: 'Italian voice',
        value: 'italian-voice',
        language: WritingLanguage.Italian,
      },
      {
        label: 'Spanish voice',
        value: 'spanish-voice',
        language: WritingLanguage.Spanish,
      },
    ]);
    expect(component['supportsSelectedTtsModelTextEmphasis']).toBeTrue();
  });

  it('resolves the TTS model from a configured voice when the model is absent', () => {
    integrationsService.getIntegrationsConfig.and.returnValue(
      of(config({ ttsModelId: '', ttsVoiceId: 'spanish-voice' })),
    );

    component.ngOnInit();

    expect(component.integrationsForm.value.ttsModelId).toBe('expressive');
    expect(component.integrationsForm.value.ttsVoiceId).toBe('spanish-voice');
  });

  it('falls back to the first voice when the selected voice is not in the model', () => {
    integrationsService.getIntegrationsConfig.and.returnValue(
      of(config({ ttsModelId: 'basic', ttsVoiceId: 'missing' })),
    );

    component.ngOnInit();

    expect(component.integrationsForm.value.ttsModelId).toBe('basic');
    expect(component.integrationsForm.value.ttsVoiceId).toBe('english-voice');
  });

  it('updates the voice and disables emphasis when a non-supporting model is selected', () => {
    component.ngOnInit();

    component.integrationsForm.controls.ttsModelId.setValue('basic');

    expect(component.integrationsForm.value.ttsVoiceId).toBe('english-voice');
    expect(component.integrationsForm.value.ttsEnableTextEmphasis).toBeFalse();
    expect(component['supportsSelectedTtsModelTextEmphasis']).toBeFalse();
  });

  it('clears TTS choices and emphasis when loading models fails', () => {
    const error = new Error('TTS models failed');
    const consoleError = spyOn(console, 'error');
    generateAudioService.getAvailableModels.and.returnValue(
      throwError(() => error),
    );

    component.ngOnInit();

    expect(consoleError).toHaveBeenCalledOnceWith(
      'Error loading TTS voices:',
      error,
    );
    expect(component.availableTtsModels).toEqual([]);
    expect(component.integrationsForm.value.ttsModelId).toBe('');
    expect(component.integrationsForm.value.ttsVoiceId).toBe('');
    expect(component.integrationsForm.value.ttsEnableTextEmphasis).toBeFalse();
  });

  it('maps each self-hosted provider to its URL control, label, and placeholder', () => {
    const cases = [
      [TtsProvider.Custom, 'customTtsBaseUrl', 'Custom TTS', 'http://localhost:5000/'],
      [TtsProvider.PocketTts, 'pocketTtsBaseUrl', 'Pocket TTS', 'http://localhost:8000/'],
      [TtsProvider.VibeVoice, 'vibeVoiceBaseUrl', 'VibeVoice', 'http://localhost:8000/'],
      [TtsProvider.Chatterbox, 'chatterboxBaseUrl', 'Chatterbox', 'http://localhost:8000/'],
      [TtsProvider.Qwen3, 'qwen3BaseUrl', 'Qwen3', 'http://localhost:8000/'],
      [TtsProvider.OmniVoice, 'omniVoiceBaseUrl', 'OmniVoice', 'http://localhost:8000/'],
      [TtsProvider.Audio8, 'audio8BaseUrl', 'Audio8', 'http://localhost:8000/'],
    ] as const;

    for (const [provider, controlName, label, placeholder] of cases) {
      component.integrationsForm.controls.ttsProvider.setValue(provider, {
        emitEvent: false,
      });

      expect(component['selectedTtsProviderBaseUrlControl']).toBe(
        component.integrationsForm.controls[controlName],
      );
      expect(component['selectedTtsProviderBaseUrlLabel']).toBe(label);
      expect(component['selectedTtsProviderBaseUrlPlaceholder']).toBe(placeholder);
    }

    component.integrationsForm.controls.ttsProvider.setValue(
      TtsProvider.ElevenLabs,
      { emitEvent: false },
    );
    expect(component['selectedTtsProviderBaseUrlControl']).toBeNull();
    expect(component['selectedTtsProviderBaseUrlLabel']).toBeNull();
  });

  it('formats provider option labels for display', () => {
    expect(component.ttsProviderOptions).toContain({
      label: 'Nano GPT',
      value: TtsProvider.NanoGpt,
    });
    expect(component.textGenerationProviderOptions).toContain({
      label: 'Google Gen AI',
      value: TextGenerationProvider.GoogleGenAi,
    });
    expect(component.imageGenerationProviderOptions).toContain({
      label: 'De API',
      value: ImageGenerationProvider.DeApi,
    });
    expect(component.videoGenerationProviderOptions).toContain({
      label: 'De API',
      value: VideoGenerationProvider.DeApi,
    });
  });

  it('loads balances only for configured API keys and clears stale values otherwise', () => {
    component.hasDeApiApiKey = false;
    component.hasNanoGptApiKey = false;
    component.deApiBalanceUsd = 1;
    component.nanoGptBalanceUsd = 2;

    component.loadConfiguredBalances();

    expect(generateAudioService.getBalanceUsd).not.toHaveBeenCalled();
    expect(component.deApiBalanceUsd).toBeNull();
    expect(component.nanoGptBalanceUsd).toBeNull();
  });

  it('stores successful DeAPI and NanoGPT balance responses', () => {
    generateAudioService.getBalanceUsd.and.callFake((provider) =>
      of(provider === TtsProvider.DeApi ? 18.25 : 7.5),
    );

    component.loadDeApiBalance();
    component.loadNanoGptBalance();

    expect(component.deApiBalanceUsd).toBe(18.25);
    expect(component.nanoGptBalanceUsd).toBe(7.5);
    expect(component.isLoadingDeApiBalance).toBeFalse();
    expect(component.isLoadingNanoGptBalance).toBeFalse();
  });

  it('clears balance loading state and value after provider errors', () => {
    const error = new Error('balance failed');
    const consoleError = spyOn(console, 'error');
    generateAudioService.getBalanceUsd.and.returnValue(
      throwError(() => error),
    );
    component.deApiBalanceUsd = 1;
    component.nanoGptBalanceUsd = 2;

    component.loadDeApiBalance();
    component.loadNanoGptBalance();

    expect(consoleError.calls.allArgs()).toEqual([
      ['Error loading DeAPI balance:', error],
      ['Error loading NanoGPT balance:', error],
    ]);
    expect(component.deApiBalanceUsd).toBeNull();
    expect(component.nanoGptBalanceUsd).toBeNull();
    expect(component.isLoadingDeApiBalance).toBeFalse();
    expect(component.isLoadingNanoGptBalance).toBeFalse();
  });

  it('requires a selected voice before previewing', async () => {
    component.integrationsForm.controls.ttsVoiceId.setValue('');

    await component.previewTtsVoice();

    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Please select a TTS voice first.',
    );
    expect(createPlayer).not.toHaveBeenCalled();
  });

  it('ignores another preview while one is already running', async () => {
    component.isPreviewingTtsVoice = true;
    component.integrationsForm.controls.ttsVoiceId.setValue('english-voice');

    await component.previewTtsVoice();

    expect(generateAudioService.textToSpeechStreamResponse).not.toHaveBeenCalled();
    expect(createPlayer).not.toHaveBeenCalled();
  });

  it('streams voice-preview chunks with language-appropriate sample text', async () => {
    const firstChunk = new Uint8Array([1, 2]);
    const secondChunk = new Uint8Array([3]);
    spyOn(console, 'time');
    spyOn(console, 'timeEnd');
    component.availableTtsModels = ttsModels();
    component.integrationsForm.patchValue({
      ttsProvider: TtsProvider.Qwen3,
      ttsModelId: 'expressive',
      ttsVoiceId: 'spanish-voice',
    });
    generateAudioService.textToSpeechStreamResponse.and.resolveTo(
      responseWithChunks(firstChunk, secondChunk),
    );

    await component.previewTtsVoice();

    expect(
      generateAudioService.textToSpeechStreamResponse,
    ).toHaveBeenCalledOnceWith({
      message:
        'Hola, este es un ejemplo rápido para previsualizar la voz seleccionada.',
      modelId: 'expressive',
      voiceId: 'spanish-voice',
      provider: TtsProvider.Qwen3,
    });
    expect(player.addChunk.calls.allArgs()).toEqual([
      [firstChunk],
      [secondChunk],
    ]);
    expect(component.isPreviewingTtsVoice).toBeFalse();
  });

  it('uses an English preview sample when the selected voice is unknown', async () => {
    spyOn(console, 'time');
    spyOn(console, 'timeEnd');
    component.availableTtsModels = ttsModels();
    component.integrationsForm.patchValue({
      ttsModelId: 'expressive',
      ttsVoiceId: 'unknown',
    });
    generateAudioService.textToSpeechStreamResponse.and.resolveTo(
      responseWithChunks(),
    );

    await component.previewTtsVoice();

    expect(
      generateAudioService.textToSpeechStreamResponse,
    ).toHaveBeenCalledOnceWith(
      jasmine.objectContaining({
        message: 'Hello, this is a quick sample to preview the selected voice.',
      }),
    );
  });

  it('reports a missing preview stream and restores its loading state', async () => {
    spyOn(console, 'time');
    spyOn(console, 'timeEnd');
    component.integrationsForm.controls.ttsVoiceId.setValue('voice');
    generateAudioService.textToSpeechStreamResponse.and.resolveTo({
      body: null,
    } as Response);

    await component.previewTtsVoice();

    expect(toastr.error).toHaveBeenCalledOnceWith(
      'No audio stream was returned.',
    );
    expect(component.isPreviewingTtsVoice).toBeFalse();
    expect(player.addChunk).not.toHaveBeenCalled();
  });

  it('reports preview failures and restores its loading state', async () => {
    const error = new Error('preview failed');
    const consoleError = spyOn(console, 'error');
    spyOn(console, 'time');
    spyOn(console, 'timeEnd');
    component.integrationsForm.controls.ttsVoiceId.setValue('voice');
    generateAudioService.textToSpeechStreamResponse.and.rejectWith(error);

    await component.previewTtsVoice();

    expect(consoleError).toHaveBeenCalledOnceWith(
      'WAV preview streaming error:',
      error,
    );
    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Could not preview voice. Please verify your TTS configuration.',
    );
    expect(component.isPreviewingTtsVoice).toBeFalse();
  });

  it('stops the previous player before starting another preview', async () => {
    const nextPlayer = jasmine.createSpyObj<StreamingWavPlayerHandle>(
      'NextStreamingWavPlayerHandle',
      ['addChunk', 'stop'],
    );
    spyOn(console, 'time');
    spyOn(console, 'timeEnd');
    createPlayer.and.returnValues(player, nextPlayer);
    component.integrationsForm.controls.ttsVoiceId.setValue('voice');
    generateAudioService.textToSpeechStreamResponse.and.callFake(
      async () => responseWithChunks(),
    );

    await component.previewTtsVoice();
    await component.previewTtsVoice();

    expect(player.stop).toHaveBeenCalledTimes(1);
    expect(createPlayer).toHaveBeenCalledTimes(2);
  });

  it('stops preview audio when destroyed', async () => {
    spyOn(console, 'time');
    spyOn(console, 'timeEnd');
    component.integrationsForm.controls.ttsVoiceId.setValue('voice');
    generateAudioService.textToSpeechStreamResponse.and.resolveTo(
      responseWithChunks(),
    );
    await component.previewTtsVoice();

    component.ngOnDestroy();

    expect(player.stop).toHaveBeenCalledTimes(1);
  });

  it('does not submit an invalid form', () => {
    component.integrationsForm.controls.openRouterApiKey.setValue('x'.repeat(1001));

    component.onSubmit();

    expect(integrationsService.updateIntegrationsConfig).not.toHaveBeenCalled();
  });

  it('normalizes optional secrets and disables unsupported emphasis on submit', () => {
    component.availableTtsModels = ttsModels();
    component.integrationsForm.patchValue({
      textGenerationProvider: TextGenerationProvider.GoogleGenAi,
      textGenerationModelId: '',
      openRouterApiKey: '',
      googleGenAiApiKey: 'google-key',
      elevenLabsApiKey: '',
      unrealSpeechApiKey: '',
      nanoGptApiKey: '',
      deApiApiKey: '',
      customTtsBaseUrl: '',
      ttsProvider: TtsProvider.Custom,
      ttsModelId: 'basic',
      ttsVoiceId: 'english-voice',
      ttsEnableTextEmphasis: true,
      ttsEnableImmersive: false,
      ttsImmersivePauseMs: 0,
      imageGenerationProvider: ImageGenerationProvider.Custom,
    });

    component.onSubmit();

    expect(integrationsService.updateIntegrationsConfig).toHaveBeenCalledOnceWith(
      jasmine.objectContaining({
        textGenerationProvider: TextGenerationProvider.GoogleGenAi,
        textGenerationModelId: undefined,
        openRouterApiKey: undefined,
        googleGenAiApiKey: 'google-key',
        customTtsBaseUrl: '',
        ttsProvider: TtsProvider.Custom,
        ttsModelId: 'basic',
        ttsVoiceId: 'english-voice',
        ttsEnableTextEmphasis: false,
        ttsEnableImmersive: false,
        ttsImmersivePauseMs: 0,
        imageGenerationProvider: ImageGenerationProvider.Custom,
      }),
    );
  });

  it('marks submitted keys configured, clears them, reloads model data, and reports success', () => {
    component.availableTtsModels = ttsModels();
    component.integrationsForm.patchValue({
      openRouterApiKey: 'open-router',
      googleGenAiApiKey: 'google',
      elevenLabsApiKey: 'eleven',
      unrealSpeechApiKey: 'unreal',
      nanoGptApiKey: 'nano',
      deApiApiKey: 'deapi',
      ttsProvider: TtsProvider.Qwen3,
      ttsModelId: 'expressive',
      ttsVoiceId: 'italian-voice',
    });

    component.onSubmit();

    expect(component.hasOpenRouterApiKey).toBeTrue();
    expect(component.hasGoogleGenAiApiKey).toBeTrue();
    expect(component.hasElevenLabsApiKey).toBeTrue();
    expect(component.hasUnrealSpeechApiKey).toBeTrue();
    expect(component.hasNanoGptApiKey).toBeTrue();
    expect(component.hasDeApiApiKey).toBeTrue();
    expect(component.integrationsForm.value.openRouterApiKey).toBeNull();
    expect(component.integrationsForm.value.googleGenAiApiKey).toBeNull();
    expect(component.integrationsForm.value.elevenLabsApiKey).toBeNull();
    expect(component.integrationsForm.value.unrealSpeechApiKey).toBeNull();
    expect(component.integrationsForm.value.nanoGptApiKey).toBeNull();
    expect(component.integrationsForm.value.deApiApiKey).toBeNull();
    expect(toastr.success).toHaveBeenCalledOnceWith(
      'Integrations configuration updated successfully.',
    );
    expect(generateAudioService.getAvailableModels).toHaveBeenCalledOnceWith(
      TtsProvider.Qwen3,
    );
    expect(generateTextService.getAvailableModelInfos).toHaveBeenCalledOnceWith(
      TextGenerationProvider.OpenRouter,
    );
    expect(generateAudioService.getBalanceUsd.calls.allArgs()).toEqual([
      [TtsProvider.DeApi],
      [TtsProvider.NanoGpt],
    ]);
  });

  it('logs submission errors without reporting success', () => {
    const error = new Error('update failed');
    const consoleError = spyOn(console, 'error');
    integrationsService.updateIntegrationsConfig.and.returnValue(
      throwError(() => error),
    );

    component.onSubmit();

    expect(consoleError).toHaveBeenCalledOnceWith(
      'Error updating configuration:',
      error,
    );
    expect(toastr.success).not.toHaveBeenCalled();
  });

  it('formats nullable and numeric USD balances', () => {
    expect(component['formatUsdBalance'](null)).toBe('$0.00');
    expect(component['formatUsdBalance'](1234.567)).toBe('$1,234.57');
  });
});
