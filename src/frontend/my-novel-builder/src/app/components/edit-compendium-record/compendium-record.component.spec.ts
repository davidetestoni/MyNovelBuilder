import { HttpClient } from '@angular/common/http';
import {
  fakeAsync,
  flushMicrotasks,
  TestBed,
} from '@angular/core/testing';
import { ToastrService } from 'ngx-toastr';
import { ConfirmationService } from 'primeng/api';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { of, Subject, throwError } from 'rxjs';
import { CompendiumService } from '../../services/compendium.service';
import { GenerateAudioService } from '../../services/generate-audio.service';
import { IntegrationsService } from '../../services/integrations.service';
import { CharacterVoiceAssignmentDto } from '../../types/dtos/compendium-record/character-voice-assignment.dto';
import { CompendiumRecordDto } from '../../types/dtos/compendium-record/compendium-record.dto';
import { CompendiumRecordMediaDto } from '../../types/dtos/compendium-record/compendium-record-media.dto';
import { TtsModelDto } from '../../types/dtos/generate/tts-model.dto';
import { IntegrationsConfigDto } from '../../types/dtos/integrations/integrations-config.dto';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { TtsProvider } from '../../types/enums/tts-provider';
import { WritingLanguage } from '../../types/enums/writing-language';
import {
  STREAMING_WAV_PLAYER_FACTORY,
  StreamingWavPlayerFactory,
  StreamingWavPlayerHandle,
} from '../../utils/streaming-wav-player.factory';
import { EditImageComponent } from '../edit-image/edit-image.component';
import { GenerateMediaComponent } from '../generate-media/generate-media.component';
import { ImageSourceSelectorComponent } from '../image-source-selector/image-source-selector.component';
import { CompendiumRecordComponent } from './compendium-record.component';

describe('CompendiumRecordComponent workflows', () => {
  const originalClipboardDescriptor = Object.getOwnPropertyDescriptor(
    navigator,
    'clipboard',
  );
  let component: CompendiumRecordComponent;
  let compendiumService: jasmine.SpyObj<CompendiumService>;
  let generateAudioService: jasmine.SpyObj<GenerateAudioService>;
  let integrationsService: jasmine.SpyObj<IntegrationsService>;
  let dialogService: jasmine.SpyObj<DialogService>;
  let confirmationService: jasmine.SpyObj<ConfirmationService>;
  let http: jasmine.SpyObj<HttpClient>;
  let toastr: jasmine.SpyObj<ToastrService>;
  let dialogClosed: Subject<unknown>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let player: jasmine.SpyObj<StreamingWavPlayerHandle>;
  let createPlayer: jasmine.Spy<StreamingWavPlayerFactory>;

  const media = (
    id: string,
    isCurrent = false,
    isVideo = false,
  ): CompendiumRecordMediaDto => ({
    id,
    url: `https://example.test/${id}`,
    isCurrent,
    isVideo,
  });

  const assignment = (
    provider = TtsProvider.ElevenLabs,
    modelId = 'model-one',
    voiceId = 'voice-one',
  ): CharacterVoiceAssignmentDto => ({
    provider,
    modelId,
    voiceId,
    voiceName: `Voice ${voiceId}`,
    updatedAt: '2026-01-01T00:00:00Z',
  });

  const record = (
    type = CompendiumRecordType.Character,
  ): CompendiumRecordDto => ({
    id: 'record-one',
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    name: 'Aria',
    aliases: 'Hero',
    type,
    context: '[appearance]Tall[/appearance]',
    contextTokenCount: 5,
    media: [media('current', true), media('other')],
    compendiumId: 'compendium-one',
    alwaysIncluded: false,
    characterVoiceAssignments: [],
  });

  const model = (
    modelId: string,
    ...voices: Array<[string, WritingLanguage]>
  ): TtsModelDto => ({
    modelId,
    name: `Model ${modelId}`,
    supportsTextEmphasis: false,
    voices: voices.map(([voiceId, language]) => ({
      voiceId,
      name: `Voice ${voiceId}`,
      previewUrl: null,
      language,
    })),
  });

  const streamFromChunks = (
    ...chunks: Uint8Array<ArrayBuffer>[]
  ): ReadableStream<Uint8Array<ArrayBuffer>> =>
    new ReadableStream<Uint8Array<ArrayBuffer>>({
      start(controller) {
        chunks.forEach((chunk) => controller.enqueue(chunk));
        controller.close();
      },
    });

  const setClipboard = (read: jasmine.Spy): void => {
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { read },
    });
  };

  const acceptConfirmation = (): void => {
    const options = confirmationService.confirm.calls.mostRecent().args[0];
    options.accept?.();
  };

  const createComponent = (
    value = record(),
  ): CompendiumRecordComponent => {
    const instance = TestBed.runInInjectionContext(
      () => new CompendiumRecordComponent(),
    );
    instance.record = value;
    instance.compendiumId = 'compendium-one';
    return instance;
  };

  beforeEach(() => {
    compendiumService = jasmine.createSpyObj<CompendiumService>(
      'CompendiumService',
      [
        'setCurrentRecordImage',
        'deleteRecordMedia',
        'uploadRecordMedia',
        'getRecord',
      ],
    );
    generateAudioService = jasmine.createSpyObj<GenerateAudioService>(
      'GenerateAudioService',
      ['getAvailableModels', 'textToSpeechStreamResponse'],
    );
    integrationsService = jasmine.createSpyObj<IntegrationsService>(
      'IntegrationsService',
      ['getIntegrationsConfig'],
    );
    dialogService = jasmine.createSpyObj<DialogService>('DialogService', [
      'open',
    ]);
    confirmationService = jasmine.createSpyObj<ConfirmationService>(
      'ConfirmationService',
      ['confirm'],
    );
    http = jasmine.createSpyObj<HttpClient>('HttpClient', ['get']);
    toastr = jasmine.createSpyObj<ToastrService>('ToastrService', ['error']);
    dialogClosed = new Subject<unknown>();
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>(
      'DynamicDialogRef',
      ['close'],
      { onClose: dialogClosed.asObservable() },
    );
    player = jasmine.createSpyObj<StreamingWavPlayerHandle>(
      'StreamingWavPlayerHandle',
      ['addChunk', 'stop'],
    );
    createPlayer = jasmine.createSpy<StreamingWavPlayerFactory>(
      'StreamingWavPlayerFactory',
    ).and.returnValue(player);

    compendiumService.setCurrentRecordImage.and.returnValue(of(undefined));
    compendiumService.deleteRecordMedia.and.returnValue(of(undefined));
    compendiumService.uploadRecordMedia.and.returnValue(of(undefined));
    compendiumService.getRecord.and.callFake(() => of(record()));
    generateAudioService.getAvailableModels.and.returnValue(of([]));
    integrationsService.getIntegrationsConfig.and.returnValue(
      of({
        ttsProvider: TtsProvider.ElevenLabs,
        ttsModelId: 'configured-model',
      } as IntegrationsConfigDto),
    );
    dialogService.open.and.returnValue(dialogRef);

    TestBed.configureTestingModule({
      providers: [
        { provide: CompendiumService, useValue: compendiumService },
        { provide: GenerateAudioService, useValue: generateAudioService },
        { provide: IntegrationsService, useValue: integrationsService },
        { provide: DialogService, useValue: dialogService },
        { provide: ConfirmationService, useValue: confirmationService },
        { provide: HttpClient, useValue: http },
        { provide: ToastrService, useValue: toastr },
        { provide: STREAMING_WAV_PLAYER_FACTORY, useValue: createPlayer },
      ],
    });

    component = createComponent();
  });

  afterEach(() => {
    if (originalClipboardDescriptor === undefined) {
      delete (navigator as { clipboard?: Clipboard }).clipboard;
    } else {
      Object.defineProperty(
        navigator,
        'clipboard',
        originalClipboardDescriptor,
      );
    }
  });

  it('initializes missing assignments without loading voices for non-characters', () => {
    const nonCharacter = record(CompendiumRecordType.Place);
    nonCharacter.characterVoiceAssignments = undefined as never;
    component = createComponent(nonCharacter);

    component.ngOnInit();

    expect(component.record.characterVoiceAssignments).toEqual([]);
    expect(integrationsService.getIntegrationsConfig).not.toHaveBeenCalled();
  });

  it('loads the configured provider and preserves its matching assignment', () => {
    component.record.characterVoiceAssignments = [
      assignment(TtsProvider.ElevenLabs, 'configured-model', 'saved-voice'),
    ];
    generateAudioService.getAvailableModels.and.returnValue(
      of(
        [
          model(
            'configured-model',
            ['first-voice', WritingLanguage.English],
            ['saved-voice', WritingLanguage.Italian],
          ),
        ],
      ),
    );

    component.ngOnInit();

    expect(component['selectedVoiceProvider']).toBe(TtsProvider.ElevenLabs);
    expect(generateAudioService.getAvailableModels).toHaveBeenCalledOnceWith(
      TtsProvider.ElevenLabs,
    );
    expect(component['selectedVoiceModelId']).toBe('configured-model');
    expect(component['selectedVoiceId']).toBe('saved-voice');
    expect(component['isLoadingVoiceModels']).toBeFalse();
  });

  it('logs an integrations loading failure without requesting models', () => {
    const error = new Error('config failed');
    const consoleError = spyOn(console, 'error');
    integrationsService.getIntegrationsConfig.and.returnValue(
      throwError(() => error),
    );

    component.ngOnInit();

    expect(consoleError).toHaveBeenCalledOnceWith(
      'Error loading integrations config:',
      error,
    );
    expect(generateAudioService.getAvailableModels).not.toHaveBeenCalled();
  });

  it('derives provider, model, and voice options', () => {
    component['availableVoiceModels'] = [
      model(
        'model-one',
        ['voice-one', WritingLanguage.English],
        ['voice-two', WritingLanguage.French],
      ),
    ];
    component['selectedVoiceModelId'] = 'model-one';

    expect(component.voiceProviderOptions).toContain({
      label: 'Eleven Labs',
      value: TtsProvider.ElevenLabs,
    });
    expect(component.voiceModelOptions).toEqual([
      { label: 'Model model-one', value: 'model-one' },
    ]);
    expect(component.voiceOptions).toEqual([
      { label: 'Voice voice-one', value: 'voice-one' },
      { label: 'Voice voice-two', value: 'voice-two' },
    ]);
    expect(component['formatTtsProviderLabel'](TtsProvider.DeApi)).toBe(
      'De API',
    );
    expect(component['formatTtsProviderLabel'](TtsProvider.NanoGpt)).toBe(
      'Nano GPT',
    );
  });

  it('adds a normalized unique alias and emits the record', () => {
    spyOn(component.updateRecord, 'emit');

    component.addAlias('Friend');

    expect(component.record.aliases).toBe('Hero, Friend');
    expect(component.updateRecord.emit).toHaveBeenCalledOnceWith(
      component.record,
    );
  });

  it('ignores duplicate aliases case-insensitively', () => {
    spyOn(component.updateRecord, 'emit');

    component.addAlias('hErO');

    expect(component.record.aliases).toBe('Hero');
    expect(component.updateRecord.emit).not.toHaveBeenCalled();
  });

  it('emits the current record on blur', () => {
    spyOn(component.updateRecord, 'emit');

    component.onBlur();

    expect(component.updateRecord.emit).toHaveBeenCalledOnceWith(
      component.record,
    );
  });

  it('clears voice choices when the provider is cleared', () => {
    component['availableVoiceModels'] = [
      model('old-model', ['old-voice', WritingLanguage.English]),
    ];
    component['selectedVoiceModelId'] = 'old-model';
    component['selectedVoiceId'] = 'old-voice';
    component['selectedVoiceProvider'] = null;

    component['onVoiceProviderChange']();

    expect(component['availableVoiceModels']).toEqual([]);
    expect(component['selectedVoiceModelId']).toBe('');
    expect(component['selectedVoiceId']).toBe('');
  });

  it('restores the assignment for a newly selected provider', () => {
    component.record.characterVoiceAssignments = [
      assignment(TtsProvider.OmniVoice, 'omni-model', 'omni-voice'),
    ];
    component['selectedVoiceProvider'] = TtsProvider.OmniVoice;
    generateAudioService.getAvailableModels.and.returnValue(
      of([
        model('omni-model', ['omni-voice', WritingLanguage.German]),
      ]),
    );

    component['onVoiceProviderChange']();

    expect(component['selectedVoiceModelId']).toBe('omni-model');
    expect(component['selectedVoiceId']).toBe('omni-voice');
  });

  it('ignores an older voice-model response after the provider changes', () => {
    const first = new Subject<TtsModelDto[]>();
    const second = new Subject<TtsModelDto[]>();
    generateAudioService.getAvailableModels.and.returnValues(first, second);

    component['selectedVoiceProvider'] = TtsProvider.ElevenLabs;
    component['onVoiceProviderChange']();
    component['selectedVoiceProvider'] = TtsProvider.OmniVoice;
    component['onVoiceProviderChange']();
    second.next([
      model('new-model', ['new-voice', WritingLanguage.English]),
    ]);
    first.next([
      model('old-model', ['old-voice', WritingLanguage.English]),
    ]);

    expect(component['selectedVoiceModelId']).toBe('new-model');
    expect(component['selectedVoiceId']).toBe('new-voice');
  });

  it('falls back to the first available model and voice', () => {
    generateAudioService.getAvailableModels.and.returnValue(
      of([
        model('fallback-model', ['fallback-voice', WritingLanguage.Spanish]),
      ]),
    );
    component['selectedVoiceProvider'] = TtsProvider.Custom;

    component['onVoiceProviderChange']();

    expect(component['selectedVoiceModelId']).toBe('fallback-model');
    expect(component['selectedVoiceId']).toBe('fallback-voice');
  });

  it('clears voice choices and loading state when model loading fails', () => {
    const error = new Error('models failed');
    const consoleError = spyOn(console, 'error');
    component['selectedVoiceProvider'] = TtsProvider.Custom;
    component['availableVoiceModels'] = [
      model('old', ['old-voice', WritingLanguage.English]),
    ];
    generateAudioService.getAvailableModels.and.returnValue(
      throwError(() => error),
    );

    component['onVoiceProviderChange']();

    expect(consoleError).toHaveBeenCalledOnceWith(
      'Error loading TTS models for character voices:',
      error,
    );
    expect(component['availableVoiceModels']).toEqual([]);
    expect(component['selectedVoiceModelId']).toBe('');
    expect(component['selectedVoiceId']).toBe('');
    expect(component['isLoadingVoiceModels']).toBeFalse();
  });

  it('selects the first voice when the model changes', () => {
    component['availableVoiceModels'] = [
      model('one', ['voice-one', WritingLanguage.English]),
      model('two', ['voice-two', WritingLanguage.Italian]),
    ];
    component['selectedVoiceModelId'] = 'two';

    component['onVoiceModelChange']();

    expect(component['selectedVoiceId']).toBe('voice-two');
  });

  it('does not save an incomplete or non-character assignment', () => {
    spyOn(component.updateRecord, 'emit');
    component['saveCharacterVoiceAssignment']();
    component.record.type = CompendiumRecordType.Place;
    component['selectedVoiceProvider'] = TtsProvider.ElevenLabs;
    component['selectedVoiceModelId'] = 'model';
    component['selectedVoiceId'] = 'voice';
    component['saveCharacterVoiceAssignment']();

    expect(component.updateRecord.emit).not.toHaveBeenCalled();
  });

  it('replaces the same provider/model assignment and sorts the result', () => {
    spyOn(component.updateRecord, 'emit');
    component.record.characterVoiceAssignments = [
      assignment(TtsProvider.OmniVoice, 'z-model', 'old-z'),
      assignment(TtsProvider.ElevenLabs, 'a-model', 'old-a'),
    ];
    component['availableVoiceModels'] = [
      model('z-model', ['new-z', WritingLanguage.English]),
    ];
    component['selectedVoiceProvider'] = TtsProvider.OmniVoice;
    component['selectedVoiceModelId'] = 'z-model';
    component['selectedVoiceId'] = 'new-z';

    component['saveCharacterVoiceAssignment']();

    expect(component.record.characterVoiceAssignments.length).toBe(2);
    expect(component.record.characterVoiceAssignments[1]).toEqual(
      jasmine.objectContaining({
        provider: TtsProvider.OmniVoice,
        modelId: 'z-model',
        voiceId: 'new-z',
        voiceName: 'Voice new-z',
      }),
    );
    expect(component.updateRecord.emit).toHaveBeenCalledOnceWith(
      component.record,
    );
  });

  it('removes one provider/model assignment and emits the record', () => {
    const removed = assignment();
    component.record.characterVoiceAssignments = [
      removed,
      assignment(TtsProvider.OmniVoice, 'other-model', 'other-voice'),
    ];
    spyOn(component.updateRecord, 'emit');

    component['removeCharacterVoiceAssignment'](removed);

    expect(component.record.characterVoiceAssignments).toEqual([
      assignment(TtsProvider.OmniVoice, 'other-model', 'other-voice'),
    ]);
    expect(component.updateRecord.emit).toHaveBeenCalledOnceWith(
      component.record,
    );
  });

  it('requires a complete selection before previewing', async () => {
    await component['previewSelectedVoice']();

    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Please select a TTS voice first.',
    );
    expect(createPlayer).not.toHaveBeenCalled();
  });

  it('streams the selected voice using its language sample', async () => {
    const first = new Uint8Array([1, 2]);
    const second = new Uint8Array([3]);
    component['availableVoiceModels'] = [
      model('model', ['voice', WritingLanguage.Italian]),
    ];
    component['selectedVoiceProvider'] = TtsProvider.ElevenLabs;
    component['selectedVoiceModelId'] = 'model';
    component['selectedVoiceId'] = 'voice';
    generateAudioService.textToSpeechStreamResponse.and.resolveTo(
      new Response(streamFromChunks(first, second)),
    );

    await component['previewSelectedVoice']();

    expect(generateAudioService.textToSpeechStreamResponse).toHaveBeenCalledWith(
      {
        message: jasmine.stringMatching(/^Ciao/),
        modelId: 'model',
        voiceId: 'voice',
        provider: TtsProvider.ElevenLabs,
      },
    );
    expect(player.addChunk.calls.allArgs()).toEqual([[first], [second]]);
    expect(component['isPreviewingVoice']).toBeFalse();
    expect(component['previewingVoiceKey']).toBeNull();
  });

  it('reports a missing audio response body', async () => {
    component['selectedVoiceProvider'] = TtsProvider.ElevenLabs;
    component['selectedVoiceModelId'] = 'model';
    component['selectedVoiceId'] = 'voice';
    generateAudioService.textToSpeechStreamResponse.and.resolveTo(
      new Response(null),
    );

    await component['previewSelectedVoice']();

    expect(toastr.error).toHaveBeenCalledOnceWith(
      'No audio stream was returned.',
    );
  });

  it('reports voice preview streaming failures and restores state', async () => {
    const error = new Error('stream failed');
    const consoleError = spyOn(console, 'error');
    component['selectedVoiceProvider'] = TtsProvider.Custom;
    component['selectedVoiceModelId'] = 'model';
    component['selectedVoiceId'] = 'voice';
    generateAudioService.textToSpeechStreamResponse.and.rejectWith(error);

    await component['previewSelectedVoice']();

    expect(consoleError).toHaveBeenCalledWith(
      'Character voice preview streaming error:',
      error,
    );
    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Could not preview voice. Please verify your TTS configuration.',
    );
    expect(component['isPreviewingVoice']).toBeFalse();
  });

  it('does not start a second preview while one is active', async () => {
    component['isPreviewingVoice'] = true;

    await component['previewVoice'](
      TtsProvider.Custom,
      'model',
      'voice',
      WritingLanguage.English,
    );

    expect(createPlayer).not.toHaveBeenCalled();
    expect(
      generateAudioService.textToSpeechStreamResponse,
    ).not.toHaveBeenCalled();
  });

  it('loads another provider to resolve an assignment language', async () => {
    const saved = assignment(TtsProvider.OmniVoice, 'model', 'voice');
    component['selectedVoiceProvider'] = TtsProvider.ElevenLabs;
    generateAudioService.getAvailableModels.and.returnValue(
      of([model('model', ['voice', WritingLanguage.German])]),
    );
    generateAudioService.textToSpeechStreamResponse.and.resolveTo(
      new Response(streamFromChunks()),
    );

    await component['previewCharacterVoiceAssignment'](saved);

    expect(generateAudioService.getAvailableModels).toHaveBeenCalledOnceWith(
      TtsProvider.OmniVoice,
    );
    expect(generateAudioService.textToSpeechStreamResponse).toHaveBeenCalledWith(
      jasmine.objectContaining({
        message: jasmine.stringMatching(/^Hallo/),
      }),
    );
  });

  it('falls back to English when assignment model loading fails', async () => {
    const consoleError = spyOn(console, 'error');
    generateAudioService.getAvailableModels.and.returnValue(
      throwError(() => new Error('models failed')),
    );
    generateAudioService.textToSpeechStreamResponse.and.resolveTo(
      new Response(streamFromChunks()),
    );

    await component['previewCharacterVoiceAssignment'](
      assignment(TtsProvider.OmniVoice),
    );

    expect(consoleError).toHaveBeenCalled();
    expect(generateAudioService.textToSpeechStreamResponse).toHaveBeenCalledWith(
      jasmine.objectContaining({
        message: jasmine.stringMatching(/^Hello/),
      }),
    );
  });

  it('tracks whether the selected voice or a saved assignment is previewing', () => {
    const saved = assignment();
    component['selectedVoiceProvider'] = saved.provider;
    component['selectedVoiceModelId'] = saved.modelId;
    component['selectedVoiceId'] = saved.voiceId;
    component['isPreviewingVoice'] = true;
    component['previewingVoiceKey'] =
      `${saved.provider}:${saved.modelId}:${saved.voiceId}`;

    expect(component['isPreviewingSelectedVoice']).toBeTrue();
    expect(component['isPreviewingAssignment'](saved)).toBeTrue();
    expect(
      component['isPreviewingAssignment'](
        assignment(TtsProvider.Custom, 'other', 'other'),
      ),
    ).toBeFalse();
  });

  it('sets the current image locally and through the service', () => {
    component.setCurrentImage('other');

    expect(component.record.media.map(({ id, isCurrent }) => [id, isCurrent]))
      .toEqual([
        ['current', false],
        ['other', true],
      ]);
    expect(
      compendiumService.setCurrentRecordImage,
    ).toHaveBeenCalledOnceWith('record-one', 'other');
  });

  it('restores the current image and reports a service failure', () => {
    compendiumService.setCurrentRecordImage.and.returnValue(
      throwError(() => new Error('failed')),
    );

    component.setCurrentImage('other');

    expect(component.record.media.map(({ id, isCurrent }) => [id, isCurrent]))
      .toEqual([
        ['current', true],
        ['other', false],
      ]);
    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Failed to set the current record image.',
    );
  });

  it('restores the initiating record when a late current-image request fails', () => {
    const response = new Subject<void>();
    const originalRecord = component.record;
    compendiumService.setCurrentRecordImage.and.returnValue(response);

    component.setCurrentImage('other');
    component.record = record(CompendiumRecordType.Place);
    component.record.id = 'record-two';
    response.error(new Error('failed'));

    expect(originalRecord.media.map(({ id, isCurrent }) => [id, isCurrent]))
      .toEqual([
        ['current', true],
        ['other', false],
      ]);
    expect(component.record.media[0].isCurrent).toBeTrue();
  });

  it('deletes media only after confirmation and service success', () => {
    component.removeMedia('other');
    expect(compendiumService.deleteRecordMedia).not.toHaveBeenCalled();

    acceptConfirmation();

    expect(compendiumService.deleteRecordMedia).toHaveBeenCalledOnceWith(
      'record-one',
      'other',
    );
    expect(component.record.media.map(({ id }) => id)).toEqual(['current']);
  });

  it('preserves media and reports a deletion failure', () => {
    compendiumService.deleteRecordMedia.and.returnValue(
      throwError(() => new Error('failed')),
    );

    component.removeMedia('other');
    acceptConfirmation();

    expect(component.record.media.map(({ id }) => id)).toEqual([
      'current',
      'other',
    ]);
    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Failed to delete the record media.',
    );
  });

  it('applies a late deletion response only to the initiating record', () => {
    const response = new Subject<void>();
    const originalRecord = component.record;
    compendiumService.deleteRecordMedia.and.returnValue(response);

    component.removeMedia('other');
    acceptConfirmation();
    component.record = record(CompendiumRecordType.Place);
    component.record.id = 'record-two';
    response.next();

    expect(originalRecord.media.map(({ id }) => id)).toEqual(['current']);
    expect(component.record.media.map(({ id }) => id)).toEqual([
      'current',
      'other',
    ]);
  });

  it('emits record deletion only after confirmation', () => {
    spyOn(component.deleteRecord, 'emit');

    component.removeRecord();
    expect(component.deleteRecord.emit).not.toHaveBeenCalled();
    acceptConfirmation();

    expect(component.deleteRecord.emit).toHaveBeenCalledOnceWith(
      component.record,
    );
  });

  it('opens the media-source selector with all supported choices', () => {
    component.openAddMediaDialog();

    expect(dialogService.open).toHaveBeenCalledOnceWith(
      ImageSourceSelectorComponent,
      jasmine.objectContaining({
        header: 'Add Media',
        data: {
          uploadLabel: 'Upload Image',
          generateLabel: 'Generate Media',
          clipboardLabel: 'Paste from Clipboard',
        },
      }),
    );
  });

  it('opens a file picker and uploads its selected image', () => {
    const file = new File(['image'], 'portrait.png', { type: 'image/png' });
    const input = document.createElement('input');
    Object.defineProperty(input, 'files', { configurable: true, value: [file] });
    spyOn(input, 'click').and.callFake(() => input.onchange?.(new Event('change')));
    const createElement = spyOn(document, 'createElement').and.returnValue(input);

    component.openAddMediaDialog();
    dialogClosed.next('upload');

    expect(createElement).toHaveBeenCalledWith('input');
    expect(compendiumService.uploadRecordMedia).toHaveBeenCalledOnceWith(
      'record-one',
      file,
      false,
    );
  });

  it('uploads a clipboard image and refreshes record media', fakeAsync(() => {
    const clipboardBlob = new Blob(['image'], { type: 'image/webp' });
    const read = jasmine.createSpy('read').and.resolveTo([
      {
        types: ['image/webp'],
        getType: jasmine.createSpy('getType').and.resolveTo(clipboardBlob),
      },
    ]);
    setClipboard(read);

    component.openAddMediaDialog();
    dialogClosed.next('clipboard');
    flushMicrotasks();

    expect(compendiumService.uploadRecordMedia).toHaveBeenCalledWith(
      'record-one',
      jasmine.any(File),
      false,
    );
    expect(compendiumService.getRecord).toHaveBeenCalledWith('record-one');
  }));

  it('reports clipboard read failures', async () => {
    const read = jasmine.createSpy('read').and.rejectWith(
      new Error('Clipboard denied'),
    );
    setClipboard(read);

    component.openAddMediaDialog();
    dialogClosed.next('clipboard');
    await Promise.resolve();
    await Promise.resolve();

    expect(toastr.error).toHaveBeenCalledOnceWith('Clipboard denied');
    expect(compendiumService.uploadRecordMedia).not.toHaveBeenCalled();
  });

  it('generates portrait media and uploads the dialog result', () => {
    const generated = new Blob(['image'], { type: 'image/jpeg' });

    component.openAddMediaDialog();
    dialogClosed.next('generate');
    expect(dialogService.open).toHaveBeenCalledWith(
      GenerateMediaComponent,
      jasmine.objectContaining({
        data: jasmine.objectContaining({
          compendiumId: 'compendium-one',
          compendiumRecordId: 'record-one',
          width: 832,
          height: 1248,
        }),
      }),
    );

    dialogClosed.next(generated);

    expect(compendiumService.uploadRecordMedia).toHaveBeenCalledWith(
      'record-one',
      jasmine.objectContaining({ name: 'generated-media.jpg' }),
      false,
    );
  });

  it('uses landscape dimensions for place media generation', () => {
    component.record.type = CompendiumRecordType.Place;

    component['generateImage']();

    expect(dialogService.open).toHaveBeenCalledWith(
      GenerateMediaComponent,
      jasmine.objectContaining({
        data: jasmine.objectContaining({ width: 1248, height: 832 }),
      }),
    );
  });

  it('prevents duplicate uploads until the current request completes', () => {
    const upload = new Subject<void>();
    compendiumService.uploadRecordMedia.and.returnValue(upload);

    component['uploadMedia'](new Blob(['one']), false);
    component['uploadMedia'](new Blob(['two']), false);

    expect(compendiumService.uploadRecordMedia).toHaveBeenCalledTimes(1);
    upload.next();
    upload.complete();
    component['uploadMedia'](new Blob(['three']), false);
    expect(compendiumService.uploadRecordMedia).toHaveBeenCalledTimes(2);
  });

  it('reports upload and subsequent refresh failures', () => {
    compendiumService.uploadRecordMedia.and.returnValue(
      throwError(() => new Error('upload failed')),
    );
    component['uploadMedia'](new Blob(['image']), false);
    expect(toastr.error).toHaveBeenCalledWith(
      'Failed to upload the record media.',
    );

    compendiumService.uploadRecordMedia.and.returnValue(of(undefined));
    compendiumService.getRecord.and.returnValue(
      throwError(() => new Error('refresh failed')),
    );
    component['uploadMedia'](new Blob(['image']), false);
    expect(toastr.error).toHaveBeenCalledWith(
      'Failed to refresh the record media.',
    );
  });

  it('does not edit video media', () => {
    component.editImage(media('video', false, true));

    expect(http.get).not.toHaveBeenCalled();
    expect(dialogService.open).not.toHaveBeenCalled();
  });

  it('downloads, edits, and uploads an image while preserving current state', () => {
    const original = media('image', true);
    http.get.and.returnValue(of(new Blob(['image'], { type: 'image/png' })));

    component.editImage(original);

    expect(http.get).toHaveBeenCalledTimes(1);
    expect(http.get.calls.mostRecent().args).toEqual([
      original.url,
      jasmine.objectContaining({ responseType: 'blob' }),
    ]);
    expect(dialogService.open).toHaveBeenCalledWith(
      EditImageComponent,
      jasmine.objectContaining({
        data: jasmine.objectContaining({ width: 832, height: 1248 }),
      }),
    );

    const edited = new Blob(['edited'], { type: 'image/webp' });
    dialogClosed.next(edited);
    expect(compendiumService.uploadRecordMedia).toHaveBeenCalledWith(
      'record-one',
      edited,
      true,
    );
  });

  it('reports an image download failure', () => {
    const error = new Error('download failed');
    const consoleError = spyOn(console, 'error');
    const image = media('image');
    http.get.and.returnValue(throwError(() => error));

    component.editImage(image);

    expect(consoleError).toHaveBeenCalledOnceWith(
      'Failed to download image',
      error,
    );
    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Failed to load the image for editing.',
    );
  });

  it('ignores a media refresh after the record changes', () => {
    const response = new Subject<CompendiumRecordDto>();
    compendiumService.getRecord.and.returnValue(response);
    spyOn(component.updateRecord, 'emit');

    component['refreshRecordMedia']();
    component.record = record(CompendiumRecordType.Place);
    component.record.id = 'record-two';
    response.next(record());

    expect(component.updateRecord.emit).not.toHaveBeenCalled();
  });

  it('stops preview work, invalidates model loads, and closes dialogs on destroy', () => {
    const models = new Subject<TtsModelDto[]>();
    component['selectedVoiceProvider'] = TtsProvider.Custom;
    generateAudioService.getAvailableModels.and.returnValue(models);
    component['onVoiceProviderChange']();
    component['previewPlayer'] = player;
    component.openAddMediaDialog();

    component.ngOnDestroy();
    models.next([
      model('late-model', ['late-voice', WritingLanguage.English]),
    ]);

    expect(player.stop).toHaveBeenCalledOnceWith();
    expect(dialogRef.close).toHaveBeenCalledOnceWith();
    expect(component['selectedVoiceModelId']).toBe('');
  });
});
