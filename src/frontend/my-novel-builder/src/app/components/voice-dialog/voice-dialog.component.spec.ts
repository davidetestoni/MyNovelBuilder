import { TestBed } from '@angular/core/testing';
import { ToastrService } from 'ngx-toastr';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { of, Subject, throwError } from 'rxjs';
import { GenerateAudioService } from '../../services/generate-audio.service';
import { LocalStorageService } from '../../services/local-storage.service';
import { SpeechTranscriptionService } from '../../services/speech-transcription.service';
import { VoiceService } from '../../services/voice.service';
import { TtsProviderDto } from '../../types/dtos/generate/tts-provider.dto';
import { VoiceDto } from '../../types/dtos/voice/voice.dto';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { TtsProvider } from '../../types/enums/tts-provider';
import { VoiceGender } from '../../types/enums/voice-gender';
import { WritingLanguage } from '../../types/enums/writing-language';
import {
  STREAMING_WAV_PLAYER_FACTORY,
  StreamingWavPlayerFactory,
  StreamingWavPlayerHandle,
} from '../../utils/streaming-wav-player.factory';
import {
  VoiceDialogComponent,
  VoiceDialogData,
} from './voice-dialog.component';

describe('VoiceDialogComponent workflows', () => {
  let component: VoiceDialogComponent;
  let voiceService: jasmine.SpyObj<VoiceService>;
  let speechTranscriptionService: jasmine.SpyObj<SpeechTranscriptionService>;
  let generateAudioService: jasmine.SpyObj<GenerateAudioService>;
  let localStorageService: jasmine.SpyObj<LocalStorageService>;
  let toastr: jasmine.SpyObj<ToastrService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let config: { data: VoiceDialogData };
  let player: jasmine.SpyObj<StreamingWavPlayerHandle>;
  let createPlayer: jasmine.Spy<StreamingWavPlayerFactory>;

  const voice = (): VoiceDto => ({
    id: 'voice-id',
    name: 'Existing voice',
    voiceGender: VoiceGender.Female,
    language: WritingLanguage.Italian,
    transcript: 'Existing transcript',
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-02T00:00:00Z',
  });

  const provider = (
    value: TtsProvider,
    supportsVoiceDesign = true,
  ): TtsProviderDto => ({
    provider: value,
    supportsVoiceDesign,
  });

  const streamFromChunks = (
    ...chunks: Uint8Array<ArrayBuffer>[]
  ): ReadableStream<Uint8Array<ArrayBuffer>> => {
    return new ReadableStream<Uint8Array<ArrayBuffer>>({
      start(controller) {
        chunks.forEach((chunk) => controller.enqueue(chunk));
        controller.close();
      },
    });
  };

  const createComponent = (
    data: VoiceDialogData = { mode: 'create' },
  ): VoiceDialogComponent => {
    config.data = data;
    return TestBed.runInInjectionContext(() => new VoiceDialogComponent());
  };

  const setValidVoiceForm = (fileName = 'voice.wav'): File => {
    const file = new File(['audio'], fileName, { type: 'audio/wav' });
    component['formGroup'].setValue({
      name: '  My voice  ',
      voiceGender: VoiceGender.Male,
      language: WritingLanguage.French,
      transcript: '  The exact sample transcript.  ',
      file,
    });
    return file;
  };

  const setValidDesignForm = (): void => {
    component['voiceDesignFormGroup'].setValue({
      provider: TtsProvider.OmniVoice,
      language: WritingLanguage.German,
      prompt: '  Read this sentence.  ',
      voiceDescription: '  A calm narrator.  ',
    });
  };

  beforeEach(() => {
    voiceService = jasmine.createSpyObj<VoiceService>('VoiceService', [
      'createVoice',
      'updateVoice',
      'getVoicePreviewStreamResponse',
      'getVoiceSampleUrl',
    ]);
    voiceService.getVoiceSampleUrl.and.returnValue('/api/voices/voice-id/sample');
    speechTranscriptionService = jasmine.createSpyObj<SpeechTranscriptionService>(
      'SpeechTranscriptionService', ['supportsLanguage', 'transcribe'],
    );
    speechTranscriptionService.supportsLanguage.and.returnValue(true);
    speechTranscriptionService.transcribe.and.resolveTo('Transcribed sample.');
    generateAudioService = jasmine.createSpyObj<GenerateAudioService>(
      'GenerateAudioService',
      ['getAvailableProviders', 'voiceDesign'],
    );
    localStorageService = jasmine.createSpyObj<LocalStorageService>(
      'LocalStorageService',
      ['getObjectForKey', 'setObjectForKey'],
    );
    toastr = jasmine.createSpyObj<ToastrService>('ToastrService', [
      'success',
      'error',
    ]);
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>('DynamicDialogRef', [
      'close',
    ]);
    config = { data: { mode: 'create' } };
    player = jasmine.createSpyObj<StreamingWavPlayerHandle>(
      'StreamingWavPlayerHandle',
      ['addChunk', 'stop'],
    );
    createPlayer = jasmine.createSpy<StreamingWavPlayerFactory>(
      'StreamingWavPlayerFactory',
    );

    generateAudioService.getAvailableProviders.and.returnValue(of([]));
    localStorageService.getObjectForKey.and.returnValue(null);
    createPlayer.and.returnValue(player);

    TestBed.configureTestingModule({
      providers: [
        { provide: VoiceService, useValue: voiceService },
        { provide: SpeechTranscriptionService, useValue: speechTranscriptionService },
        { provide: GenerateAudioService, useValue: generateAudioService },
        { provide: LocalStorageService, useValue: localStorageService },
        { provide: ToastrService, useValue: toastr },
        { provide: DynamicDialogRef, useValue: dialogRef },
        { provide: DynamicDialogConfig, useValue: config },
        { provide: STREAMING_WAV_PLAYER_FACTORY, useValue: createPlayer },
      ],
    });

    component = createComponent();
  });

  it('transcribes the selected file into an editable, dirty transcript', async () => {
    const file = setValidVoiceForm();
    await component['transcribeVoice']();
    expect(speechTranscriptionService.transcribe).toHaveBeenCalledOnceWith(
      file, WritingLanguage.French, jasmine.any(AbortSignal), jasmine.any(Function),
    );
    expect(component['formGroup'].controls.transcript.value).toBe('Transcribed sample.');
    expect(component['formGroup'].controls.transcript.dirty).toBeTrue();
    expect(component['isTranscribing']).toBeFalse();
  });

  it('transcribes saved samples when editing without a replacement file', async () => {
    component = createComponent({ mode: 'edit', voice: voice() });
    const fetchSample = spyOn(window, 'fetch').and.resolveTo(new Response(new Blob(['sample'])));
    await component['transcribeVoice']();
    expect(fetchSample).toHaveBeenCalledOnceWith('/api/voices/voice-id/sample', { signal: jasmine.any(AbortSignal) });
    expect(voiceService.getVoicePreviewStreamResponse).not.toHaveBeenCalled();
    expect(speechTranscriptionService.transcribe).toHaveBeenCalled();
    expect(component['formGroup'].controls.transcript.value).toBe('Transcribed sample.');
  });

  it('decodes the saved WAV through the real browser transcription service without a new upload', async () => {
    component = createComponent({ mode: 'edit', voice: voice() });
    const bytes = new ArrayBuffer(44 + 16000 * 2);
    const view = new DataView(bytes);
    const text = (offset: number, value: string) => {
      for (let index = 0; index < value.length; index++) view.setUint8(offset + index, value.charCodeAt(index));
    };
    text(0, 'RIFF'); view.setUint32(4, bytes.byteLength - 8, true);
    text(8, 'WAVE'); text(12, 'fmt '); view.setUint32(16, 16, true);
    view.setUint16(20, 1, true); view.setUint16(22, 1, true);
    view.setUint32(24, 16000, true); view.setUint32(28, 32000, true);
    view.setUint16(32, 2, true); view.setUint16(34, 16, true);
    text(36, 'data'); view.setUint32(40, bytes.byteLength - 44, true);
    spyOn(window, 'fetch').and.resolveTo(new Response(bytes, { headers: { 'Content-Type': 'audio/wav' } }));
    const browserService = new SpeechTranscriptionService();
    speechTranscriptionService.transcribe.and.callFake(browserService.transcribe.bind(browserService));
    const fakeWorker = {
      onmessage: null as ((event: MessageEvent) => void) | null,
      onerror: null,
      terminate: jasmine.createSpy('terminate'),
      postMessage: (data: { samples: Float32Array; language: string }) => {
        expect(data.samples.length).toBe(16000);
        expect(data.language).toBe('it');
        fakeWorker.onmessage!(new MessageEvent('message', { data: { text: 'Saved sample words.' } }));
      },
    };
    spyOn(window, 'Worker').and.returnValue(fakeWorker as unknown as Worker);
    await component['transcribeVoice']();
    expect(component['formGroup'].controls.file.value).toBeNull();
    expect(component['formGroup'].controls.transcript.value).toBe('Saved sample words.');
    expect(toastr.error).not.toHaveBeenCalled();
    expect(fakeWorker.terminate).toHaveBeenCalledTimes(1);
  });

  it('reports saved sample HTTP errors before attempting to decode them', async () => {
    component = createComponent({ mode: 'edit', voice: voice() });
    spyOn(window, 'fetch').and.resolveTo(new Response('Not found', { status: 404 }));
    await component['transcribeVoice']();
    expect(toastr.error).toHaveBeenCalledOnceWith('Could not load the saved voice sample (HTTP 404).');
    expect(speechTranscriptionService.transcribe).not.toHaveBeenCalled();
    expect(component['formGroup'].controls.transcript.value).toBe('Existing transcript');
  });

  it('does not transcribe without audio or for an unsupported language', async () => {
    await component['transcribeVoice']();
    setValidVoiceForm();
    speechTranscriptionService.supportsLanguage.and.returnValue(false);
    await component['transcribeVoice']();
    expect(component['canTranscribe']).toBeFalse();
    expect(speechTranscriptionService.transcribe).not.toHaveBeenCalled();
  });

  it('keeps the existing transcript for silence or transcription errors', async () => {
    setValidVoiceForm();
    speechTranscriptionService.transcribe.and.resolveTo('');
    await component['transcribeVoice']();
    expect(component['formGroup'].controls.transcript.value).toBe('  The exact sample transcript.  ');
    speechTranscriptionService.transcribe.and.rejectWith(new Error('Model failed'));
    await component['transcribeVoice']();
    expect(toastr.error).toHaveBeenCalledWith('Model failed');
    expect(component['formGroup'].controls.transcript.value).toBe('  The exact sample transcript.  ');
    expect(component['isTranscribing']).toBeFalse();
  });

  it('cancels transcription on destruction and ignores late results', async () => {
    setValidVoiceForm();
    let complete!: (text: string) => void;
    speechTranscriptionService.transcribe.and.returnValue(new Promise(resolve => { complete = resolve; }));
    const pending = component['transcribeVoice']();
    const signal = speechTranscriptionService.transcribe.calls.mostRecent().args[2];
    component.ngOnDestroy();
    expect(signal.aborted).toBeTrue();
    complete('Late result');
    await pending;
    expect(component['formGroup'].controls.transcript.value).toBe('  The exact sample transcript.  ');
    expect(toastr.error).not.toHaveBeenCalled();
  });

  it('does not overwrite edits made while transcription was running', async () => {
    setValidVoiceForm();
    let complete!: (text: string) => void;
    speechTranscriptionService.transcribe.and.returnValue(new Promise(resolve => { complete = resolve; }));
    const pending = component['transcribeVoice']();
    component['formGroup'].controls.transcript.setValue('Manual correction');
    complete('Old result');
    await pending;
    expect(component['formGroup'].controls.transcript.value).toBe('Manual correction');
  });

  it('prevents duplicate transcription and saving while transcription is active', async () => {
    setValidVoiceForm();
    let complete!: (text: string) => void;
    speechTranscriptionService.transcribe.and.returnValue(new Promise(resolve => { complete = resolve; }));
    const pending = component['transcribeVoice']();
    await component['transcribeVoice']();
    component['submit']();
    expect(speechTranscriptionService.transcribe).toHaveBeenCalledTimes(1);
    expect(voiceService.createVoice).not.toHaveBeenCalled();
    complete('Done');
    await pending;
  });

  it('offers the full saved sample without loading transcription', () => {
    component = createComponent({ mode: 'edit', voice: voice() });
    component.ngOnInit();
    expect(component['sampleAudioUrl']).toBe('/api/voices/voice-id/sample');
    expect(voiceService.getVoiceSampleUrl).toHaveBeenCalledOnceWith('voice-id');
    expect(voiceService.getVoicePreviewStreamResponse).not.toHaveBeenCalled();
    expect(speechTranscriptionService.transcribe).not.toHaveBeenCalled();
  });

  it('uses a local playback URL for selected files and releases it when replaced or destroyed', () => {
    const createUrl = spyOn(URL, 'createObjectURL').and.returnValues('blob:first', 'blob:second');
    const revokeUrl = spyOn(URL, 'revokeObjectURL');
    const first = new File(['first'], 'first.wav');
    const second = new File(['second'], 'second.wav');
    component['onFileSelected']({ target: { files: [first] } } as unknown as Event);
    expect(component['sampleAudioUrl']).toBe('blob:first');
    expect(createUrl).toHaveBeenCalledWith(first);
    component['onFileSelected']({ target: { files: [second] } } as unknown as Event);
    expect(revokeUrl).toHaveBeenCalledWith('blob:first');
    expect(component['sampleAudioUrl']).toBe('blob:second');
    component.ngOnDestroy();
    expect(revokeUrl).toHaveBeenCalledWith('blob:second');
  });

  it('restores saved sample playback after clearing a replacement file', () => {
    component = createComponent({ mode: 'edit', voice: voice() });
    spyOn(URL, 'createObjectURL').and.returnValue('blob:replacement');
    const revokeUrl = spyOn(URL, 'revokeObjectURL');
    component['onFileSelected']({ target: { files: [new File(['sample'], 'new.wav')] } } as unknown as Event);
    component['onFileSelected']({ target: { files: [] } } as unknown as Event);
    expect(component['sampleAudioUrl']).toBe('/api/voices/voice-id/sample');
    expect(revokeUrl).toHaveBeenCalledWith('blob:replacement');
  });

  it('renders native audio controls beside the transcript in the edit dialog', () => {
    config.data = { mode: 'edit', voice: voice() };
    const fixture = TestBed.createComponent(VoiceDialogComponent);
    fixture.detectChanges();
    const audio: HTMLAudioElement = fixture.nativeElement.querySelector('audio');
    expect(audio).not.toBeNull();
    expect(audio.controls).toBeTrue();
    expect(audio.preload).toBe('metadata');
    expect(audio.getAttribute('src')).toBe('/api/voices/voice-id/sample');
    expect(fixture.nativeElement.querySelector('.voice-details-column audio')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.voice-transcript-column textarea')).not.toBeNull();
    expect(speechTranscriptionService.transcribe).not.toHaveBeenCalled();
    fixture.destroy();
  });

  it('starts a create dialog with required defaults', () => {
    expect(component['data']).toEqual({ mode: 'create' });
    expect(component['formGroup'].getRawValue()).toEqual({
      name: '',
      voiceGender: VoiceGender.Both,
      language: WritingLanguage.English,
      transcript: '',
      file: null,
    });
    expect(component['formGroup'].invalid).toBeTrue();
    expect(component['voiceGenderOptions']).toEqual([
      { label: 'Both', value: VoiceGender.Both },
      { label: 'Male', value: VoiceGender.Male },
      { label: 'Female', value: VoiceGender.Female },
    ]);
  });

  it('prepopulates editable metadata without requiring a replacement file', () => {
    component = createComponent({ mode: 'edit', voice: voice() });

    expect(component['formGroup'].getRawValue()).toEqual({
      name: 'Existing voice',
      voiceGender: VoiceGender.Female,
      language: WritingLanguage.Italian,
      transcript: 'Existing transcript',
      file: null,
    });
    expect(component['formGroup'].valid).toBeTrue();
  });

  it('restores the design draft and loads only providers that support voice design', () => {
    localStorageService.getObjectForKey.and.returnValue({
      provider: TtsProvider.OmniVoice,
      language: WritingLanguage.Italian,
      prompt: 'Stored prompt',
      voiceDescription: 'Stored description',
    });
    generateAudioService.getAvailableProviders.and.returnValue(
      of([
        provider(TtsProvider.Chatterbox, false),
        provider(TtsProvider.OmniVoice),
        provider(TtsProvider.ElevenLabs),
      ]),
    );

    component.ngOnInit();

    expect(localStorageService.getObjectForKey).toHaveBeenCalledOnceWith(
      LocalStorageKey.VoiceDesignDraft,
    );
    expect(component['availableVoiceDesignProviders']).toEqual([
      provider(TtsProvider.OmniVoice),
      provider(TtsProvider.ElevenLabs),
    ]);
    expect(component['voiceDesignFormGroup'].getRawValue()).toEqual({
      provider: TtsProvider.OmniVoice,
      language: WritingLanguage.Italian,
      prompt: 'Stored prompt',
      voiceDescription: 'Stored description',
    });
  });

  it('falls back to the first supported provider when the draft provider is unavailable', () => {
    localStorageService.getObjectForKey.and.returnValue({
      provider: TtsProvider.Custom,
      language: WritingLanguage.English,
      prompt: 'Stored prompt',
      voiceDescription: 'Stored description',
    });
    generateAudioService.getAvailableProviders.and.returnValue(
      of([provider(TtsProvider.ElevenLabs)]),
    );

    component.ngOnInit();

    expect(component['voiceDesignFormGroup'].controls.provider.value).toBe(
      TtsProvider.ElevenLabs,
    );
  });

  it('clears provider options when loading providers fails', () => {
    const error = new Error('provider failure');
    const consoleError = spyOn(console, 'error');
    generateAudioService.getAvailableProviders.and.returnValue(
      throwError(() => error),
    );
    component['availableVoiceDesignProviders'] = [
      provider(TtsProvider.OmniVoice),
    ];

    component.ngOnInit();

    expect(consoleError).toHaveBeenCalledOnceWith(
      'Error loading TTS providers:',
      error,
    );
    expect(component['availableVoiceDesignProviders']).toEqual([]);
  });

  it('persists design changes after initialization', () => {
    component.ngOnInit();

    component['voiceDesignFormGroup'].setValue({
      provider: TtsProvider.OmniVoice,
      language: WritingLanguage.Spanish,
      prompt: 'A phrase',
      voiceDescription: 'A description',
    });

    expect(localStorageService.setObjectForKey).toHaveBeenCalledWith(
      LocalStorageKey.VoiceDesignDraft,
      {
        provider: TtsProvider.OmniVoice,
        language: WritingLanguage.Spanish,
        prompt: 'A phrase',
        voiceDescription: 'A description',
      },
    );
  });

  it('formats provider options and exposes the Omni Voice design hint', () => {
    component['availableVoiceDesignProviders'] = [
      provider(TtsProvider.ElevenLabs),
      provider(TtsProvider.DeApi),
      provider(TtsProvider.NanoGpt),
    ];

    expect(component['voiceDesignProviderOptions']).toEqual([
      { label: 'Eleven Labs', value: TtsProvider.ElevenLabs },
      { label: 'De API', value: TtsProvider.DeApi },
      { label: 'Nano GPT', value: TtsProvider.NanoGpt },
    ]);

    component['voiceDesignFormGroup'].controls.provider.setValue(
      TtsProvider.OmniVoice,
    );
    expect(component['currentVoiceDesignHint']).toContain(
      'Valid English items:',
    );
    component['voiceDesignFormGroup'].controls.provider.setValue(
      TtsProvider.ElevenLabs,
    );
    expect(component['currentVoiceDesignHint']).toBeNull();
  });

  it('opens voice design with the current language and clears an old sample', () => {
    component['availableVoiceDesignProviders'] = [
      provider(TtsProvider.OmniVoice),
    ];
    component['formGroup'].controls.language.setValue(WritingLanguage.French);
    component['generatedVoiceSample'] = new Blob(['old']);
    component['generatedVoiceSampleFileName'] = 'old.wav';

    component['openVoiceDesignDialog']();

    expect(component['isVoiceDesignDialogVisible']).toBeTrue();
    expect(component['generatedVoiceSample']).toBeNull();
    expect(component['generatedVoiceSampleFileName']).toBe('');
    expect(component['voiceDesignFormGroup'].controls.language.value).toBe(
      WritingLanguage.French,
    );
    expect(component['voiceDesignFormGroup'].controls.provider.value).toBe(
      TtsProvider.OmniVoice,
    );
  });

  it('closes voice design and stops an active preview', async () => {
    const sample = new Blob([]);
    spyOn(sample, 'stream').and.returnValue(streamFromChunks());
    component['generatedVoiceSample'] = sample;
    await component['previewDesignedVoice']();
    component['isPreviewingDesignedVoice'] = true;
    component['isVoiceDesignDialogVisible'] = true;

    component['closeVoiceDesignDialog']();

    expect(player.stop).toHaveBeenCalledTimes(1);
    expect(component['isPreviewingDesignedVoice']).toBeFalse();
    expect(component['isVoiceDesignDialogVisible']).toBeFalse();
  });

  it('does not generate from an invalid design form', () => {
    component['generateDesignedVoice']();

    expect(generateAudioService.voiceDesign).not.toHaveBeenCalled();
    expect(component['isGeneratingDesignedVoice']).toBeFalse();
  });

  it('generates a sample with normalized input and updates its state', () => {
    const sample = new Blob(['audio'], { type: 'audio/wav' });
    generateAudioService.voiceDesign.and.returnValue(of(sample));
    setValidDesignForm();

    component['generateDesignedVoice']();

    expect(generateAudioService.voiceDesign).toHaveBeenCalledOnceWith({
      provider: TtsProvider.OmniVoice,
      prompt: 'Read this sentence.',
      language: WritingLanguage.German,
      voiceDescription: 'A calm narrator.',
    });
    expect(component['generatedVoiceSample']).toBe(sample);
    expect(component['generatedVoiceSampleFileName']).toBe(
      'omniVoice-voice-design.wav',
    );
    expect(component['formGroup'].controls.language.value).toBe(
      WritingLanguage.German,
    );
    expect(component['isGeneratingDesignedVoice']).toBeFalse();
    expect(toastr.success).toHaveBeenCalledOnceWith(
      'Voice sample generated. Preview it, then apply it if it sounds right.',
    );
  });

  it('prevents duplicate generation while a request is pending', () => {
    const response = new Subject<Blob>();
    generateAudioService.voiceDesign.and.returnValue(response);
    setValidDesignForm();

    component['generateDesignedVoice']();
    component['generateDesignedVoice']();

    expect(generateAudioService.voiceDesign).toHaveBeenCalledTimes(1);
    expect(component['isGeneratingDesignedVoice']).toBeTrue();

    response.next(new Blob(['audio']));
    expect(component['isGeneratingDesignedVoice']).toBeFalse();
  });

  it('reports design generation errors and permits another attempt', () => {
    const error = new Error('generation failed');
    const consoleError = spyOn(console, 'error');
    generateAudioService.voiceDesign.and.returnValue(
      throwError(() => error),
    );
    setValidDesignForm();

    component['generateDesignedVoice']();

    expect(consoleError).toHaveBeenCalledOnceWith(
      'Voice design generation error:',
      error,
    );
    expect(component['isGeneratingDesignedVoice']).toBeFalse();
    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Could not generate the voice sample.',
    );
  });

  it('streams every generated sample chunk through the preview player', async () => {
    const firstChunk = new Uint8Array([1, 2]);
    const secondChunk = new Uint8Array([3, 4]);
    const sample = new Blob([]);
    spyOn(sample, 'stream').and.returnValue(
      streamFromChunks(firstChunk, secondChunk),
    );
    component['generatedVoiceSample'] = sample;

    await component['previewDesignedVoice']();

    expect(createPlayer).toHaveBeenCalledTimes(1);
    expect(player.addChunk.calls.allArgs()).toEqual([
      [firstChunk],
      [secondChunk],
    ]);
    expect(component['isPreviewingDesignedVoice']).toBeFalse();
  });

  it('does not start a preview without a sample or while already previewing', async () => {
    await component['previewDesignedVoice']();
    component['generatedVoiceSample'] = new Blob(['audio']);
    component['isPreviewingDesignedVoice'] = true;
    await component['previewDesignedVoice']();

    expect(createPlayer).not.toHaveBeenCalled();
  });

  it('reports sample preview failures and restores its state', async () => {
    const error = new Error('stream failure');
    const consoleError = spyOn(console, 'error');
    const sample = new Blob([]);
    spyOn(sample, 'stream').and.throwError(error);
    component['generatedVoiceSample'] = sample;

    await component['previewDesignedVoice']();

    expect(consoleError).toHaveBeenCalledOnceWith(
      'Designed voice preview error:',
      error,
    );
    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Could not preview the generated voice sample.',
    );
    expect(component['isPreviewingDesignedVoice']).toBeFalse();
  });

  it('applies the generated sample as a dirty WAV file and closes design', () => {
    const sample = new Blob(['audio'], { type: 'audio/wav' });
    component['generatedVoiceSample'] = sample;
    component['generatedVoiceSampleFileName'] = 'designed.wav';
    component['voiceDesignFormGroup'].controls.prompt.setValue('  Designed sample words.  ');
    component['isVoiceDesignDialogVisible'] = true;

    component['applyDesignedVoice']();

    const file = component['formGroup'].controls.file.value;
    expect(file).toEqual(jasmine.any(File));
    expect(file?.name).toBe('designed.wav');
    expect(file?.type).toBe('audio/wav');
    expect(component['sampleAudioUrl']).toMatch(/^blob:/);
    expect(component['formGroup'].controls.file.dirty).toBeTrue();
    expect(component['selectedFileName']).toBe('designed.wav');
    expect(component['formGroup'].controls.transcript.value).toBe(
      'Designed sample words.',
    );
    expect(component['isVoiceDesignDialogVisible']).toBeFalse();
  });

  it('accepts a selected WAV file and marks the field dirty', () => {
    const file = new File(['audio'], 'VOICE.WAV', { type: 'audio/wav' });

    component['onFileSelected']({
      target: { files: [file], value: 'VOICE.WAV' },
    } as unknown as Event);

    expect(component['formGroup'].controls.file.value).toBe(file);
    expect(component['formGroup'].controls.file.dirty).toBeTrue();
    expect(component['selectedFileName']).toBe('VOICE.WAV');
    expect(toastr.error).not.toHaveBeenCalled();
  });

  it('rejects a non-WAV selection and clears the native input', () => {
    const file = new File(['audio'], 'voice.mp3', { type: 'audio/mpeg' });
    const input = { files: [file], value: 'voice.mp3' };

    component['onFileSelected']({
      target: input,
    } as unknown as Event);

    expect(component['formGroup'].controls.file.value).toBeNull();
    expect(component['selectedFileName']).toBe('');
    expect(input.value).toBe('');
    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Only .wav files are allowed.',
    );
  });

  it('clears the selected file when the picker returns no file', () => {
    setValidVoiceForm();

    component['onFileSelected']({
      target: { files: [], value: '' },
    } as unknown as Event);

    expect(component['formGroup'].controls.file.value).toBeNull();
    expect(component['selectedFileName']).toBe('');
  });

  it('creates a voice with normalized values and closes after success', () => {
    const file = setValidVoiceForm();
    voiceService.createVoice.and.returnValue(of(voice()));

    component['submit']();

    expect(voiceService.createVoice).toHaveBeenCalledOnceWith(
      'My voice',
      VoiceGender.Male,
      WritingLanguage.French,
      'The exact sample transcript.',
      file,
    );
    expect(toastr.success).toHaveBeenCalledOnceWith(
      'Voice created successfully.',
    );
    expect(dialogRef.close).toHaveBeenCalledOnceWith(true);
    expect(voiceService.updateVoice).not.toHaveBeenCalled();
  });

  it('updates the selected voice and closes after success', () => {
    const existingVoice = voice();
    component = createComponent({ mode: 'edit', voice: existingVoice });
    const file = setValidVoiceForm();
    voiceService.updateVoice.and.returnValue(of(existingVoice));

    component['submit']();

    expect(voiceService.updateVoice).toHaveBeenCalledOnceWith(
      'voice-id',
      'My voice',
      VoiceGender.Male,
      WritingLanguage.French,
      'The exact sample transcript.',
      file,
    );
    expect(toastr.success).toHaveBeenCalledOnceWith(
      'Voice updated successfully.',
    );
    expect(dialogRef.close).toHaveBeenCalledOnceWith(true);
    expect(voiceService.createVoice).not.toHaveBeenCalled();
  });

  it('updates metadata without replacing the existing WAV file', () => {
    const existingVoice = voice();
    component = createComponent({ mode: 'edit', voice: existingVoice });
    component['formGroup'].controls.name.setValue('Updated metadata');
    voiceService.updateVoice.and.returnValue(of(existingVoice));

    component['submit']();

    expect(voiceService.updateVoice).toHaveBeenCalledOnceWith(
      'voice-id',
      'Updated metadata',
      VoiceGender.Female,
      WritingLanguage.Italian,
      'Existing transcript',
      null,
    );
    expect(dialogRef.close).toHaveBeenCalledOnceWith(true);
  });

  it('does not submit invalid or whitespace-only form values', () => {
    component['submit']();
    setValidVoiceForm();
    component['formGroup'].controls.name.setValue('   ');
    component['submit']();

    expect(voiceService.createVoice).not.toHaveBeenCalled();
    expect(voiceService.updateVoice).not.toHaveBeenCalled();
    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('closes without a result when cancelled and stops audio when destroyed', async () => {
    const sample = new Blob([]);
    spyOn(sample, 'stream').and.returnValue(streamFromChunks());
    component['generatedVoiceSample'] = sample;
    await component['previewDesignedVoice']();

    component['cancel']();
    component.ngOnDestroy();

    expect(dialogRef.close).toHaveBeenCalledOnceWith();
    expect(player.stop).toHaveBeenCalledTimes(1);
  });
});
