import { TestBed } from '@angular/core/testing';
import { ToastrService } from 'ngx-toastr';
import { Confirmation, ConfirmationService } from 'primeng/api';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { of, Subject } from 'rxjs';
import { VoiceDialogComponent } from '../../components/voice-dialog/voice-dialog.component';
import { VoiceService } from '../../services/voice.service';
import { VoiceDto } from '../../types/dtos/voice/voice.dto';
import { VoiceGender } from '../../types/enums/voice-gender';
import { WritingLanguage } from '../../types/enums/writing-language';
import {
  STREAMING_WAV_PLAYER_FACTORY,
  StreamingWavPlayerFactory,
  StreamingWavPlayerHandle,
} from '../../utils/streaming-wav-player.factory';
import { VoicesComponent } from './voices.component';

describe('VoicesComponent workflows', () => {
  let component: VoicesComponent;
  let voiceService: jasmine.SpyObj<VoiceService>;
  let toastr: jasmine.SpyObj<ToastrService>;
  let dialogService: jasmine.SpyObj<DialogService>;
  let confirmationService: jasmine.SpyObj<ConfirmationService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let dialogClosed: Subject<boolean | undefined>;
  let player: jasmine.SpyObj<StreamingWavPlayerHandle>;
  let createPlayer: jasmine.Spy<StreamingWavPlayerFactory>;

  const voice = (
    id: string,
    voiceGender = VoiceGender.Both,
  ): VoiceDto => ({
    id,
    name: `Voice ${id}`,
    voiceGender,
    language: WritingLanguage.English,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-02T00:00:00Z',
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
    voiceService = jasmine.createSpyObj<VoiceService>('VoiceService', [
      'getVoices',
      'deleteVoice',
      'getVoicePreviewStreamResponse',
    ]);
    toastr = jasmine.createSpyObj<ToastrService>('ToastrService', [
      'success',
      'error',
    ]);
    dialogService = jasmine.createSpyObj<DialogService>('DialogService', [
      'open',
    ]);
    confirmationService = jasmine.createSpyObj<ConfirmationService>(
      'ConfirmationService',
      ['confirm'],
    );
    dialogClosed = new Subject<boolean | undefined>();
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
    );

    voiceService.getVoices.and.returnValue(of([]));
    voiceService.deleteVoice.and.returnValue(of(undefined));
    dialogService.open.and.returnValue(dialogRef);
    createPlayer.and.returnValue(player);

    TestBed.configureTestingModule({
      providers: [
        { provide: VoiceService, useValue: voiceService },
        { provide: ToastrService, useValue: toastr },
        { provide: DialogService, useValue: dialogService },
        { provide: ConfirmationService, useValue: confirmationService },
        { provide: STREAMING_WAV_PLAYER_FACTORY, useValue: createPlayer },
      ],
    });

    component = TestBed.runInInjectionContext(() => new VoicesComponent());
  });

  it('loads voices on initialization', () => {
    const voices = [voice('one'), voice('two')];
    voiceService.getVoices.and.returnValue(of(voices));

    component.ngOnInit();

    expect(voiceService.getVoices).toHaveBeenCalledTimes(1);
    expect(component.voices).toBe(voices);
  });

  it('returns a display label for every voice gender', () => {
    expect(component.getGenderLabel(VoiceGender.Male)).toBe('Male');
    expect(component.getGenderLabel(VoiceGender.Female)).toBe('Female');
    expect(component.getGenderLabel(VoiceGender.Both)).toBe('Both');
  });

  it('streams a preview through the audio player and resets its loading state', async () => {
    const firstChunk = new Uint8Array([1, 2]);
    const secondChunk = new Uint8Array([3, 4]);
    voiceService.getVoicePreviewStreamResponse.and.resolveTo(
      responseWithChunks(firstChunk, secondChunk),
    );

    await component.previewVoice('voice-id');

    expect(voiceService.getVoicePreviewStreamResponse).toHaveBeenCalledOnceWith(
      'voice-id',
      3,
    );
    expect(createPlayer).toHaveBeenCalledTimes(1);
    expect(player.addChunk.calls.allArgs()).toEqual([
      [firstChunk],
      [secondChunk],
    ]);
    expect(component.previewingVoiceId).toBeNull();
  });

  it('ignores another preview while one is already in progress', async () => {
    component.previewingVoiceId = 'first';

    await component.previewVoice('second');

    expect(voiceService.getVoicePreviewStreamResponse).not.toHaveBeenCalled();
    expect(createPlayer).not.toHaveBeenCalled();
  });

  it('reports a missing preview stream and restores the loading state', async () => {
    voiceService.getVoicePreviewStreamResponse.and.resolveTo({
      body: null,
    } as Response);

    await component.previewVoice('voice-id');

    expect(toastr.error).toHaveBeenCalledOnceWith(
      'No audio stream was returned.',
    );
    expect(component.previewingVoiceId).toBeNull();
    expect(player.addChunk).not.toHaveBeenCalled();
  });

  it('reports preview failures and restores the loading state', async () => {
    const error = new Error('preview failed');
    const consoleError = spyOn(console, 'error');
    voiceService.getVoicePreviewStreamResponse.and.rejectWith(error);

    await component.previewVoice('voice-id');

    expect(consoleError).toHaveBeenCalledOnceWith(
      'Voice preview streaming error:',
      error,
    );
    expect(toastr.error).toHaveBeenCalledOnceWith('Could not preview voice.');
    expect(component.previewingVoiceId).toBeNull();
  });

  it('stops the previous player before starting another preview', async () => {
    const nextPlayer = jasmine.createSpyObj<StreamingWavPlayerHandle>(
      'NextStreamingWavPlayerHandle',
      ['addChunk', 'stop'],
    );
    createPlayer.and.returnValues(player, nextPlayer);
    voiceService.getVoicePreviewStreamResponse.and.callFake(
      async () => responseWithChunks(),
    );

    await component.previewVoice('first');
    await component.previewVoice('second');

    expect(player.stop).toHaveBeenCalledTimes(1);
    expect(createPlayer).toHaveBeenCalledTimes(2);
  });

  it('opens the create dialog and reloads only after a successful close', () => {
    component.openCreateVoiceDialog();

    expect(dialogService.open).toHaveBeenCalledOnceWith(VoiceDialogComponent, {
      header: 'Create Voice',
      width: '35rem',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      modal: true,
      closable: true,
      closeOnEscape: true,
      dismissableMask: true,
      data: { mode: 'create' },
    });

    dialogClosed.next(undefined);
    expect(voiceService.getVoices).not.toHaveBeenCalled();

    dialogClosed.next(true);
    expect(voiceService.getVoices).toHaveBeenCalledTimes(1);
  });

  it('opens the edit dialog with the voice and suppresses the triggering event', () => {
    const selectedVoice = voice('selected');
    const event = jasmine.createSpyObj<Event>('Event', [
      'stopPropagation',
      'preventDefault',
    ]);

    component.openEditVoiceDialog(selectedVoice, event);

    expect(event.stopPropagation).toHaveBeenCalledTimes(1);
    expect(event.preventDefault).toHaveBeenCalledTimes(1);
    expect(dialogService.open).toHaveBeenCalledOnceWith(VoiceDialogComponent, {
      header: 'Edit Voice',
      width: '35rem',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      modal: true,
      closable: true,
      closeOnEscape: true,
      dismissableMask: true,
      data: { mode: 'edit', voice: selectedVoice },
    });

    dialogClosed.next(true);
    expect(voiceService.getVoices).toHaveBeenCalledTimes(1);
  });

  it('deletes a voice only after confirmation and then reloads the list', () => {
    const selectedVoice = voice('selected');
    const event = jasmine.createSpyObj<Event>('Event', [
      'stopPropagation',
      'preventDefault',
    ]);

    component.deleteVoice(selectedVoice, event);

    expect(event.stopPropagation).toHaveBeenCalledTimes(1);
    expect(event.preventDefault).toHaveBeenCalledTimes(1);
    expect(voiceService.deleteVoice).not.toHaveBeenCalled();

    const confirmation = confirmationService.confirm.calls.mostRecent()
      .args[0] as Confirmation;
    expect(confirmation).toEqual(
      jasmine.objectContaining({
        message:
          'Are you sure you want to delete this voice? This action cannot be undone.',
        header: 'Confirm Voice Deletion',
        icon: 'pi pi-exclamation-triangle',
        acceptButtonStyleClass: 'p-button-danger',
      }),
    );

    confirmation.accept?.();

    expect(voiceService.deleteVoice).toHaveBeenCalledOnceWith('selected');
    expect(toastr.success).toHaveBeenCalledOnceWith('Voice deleted.');
    expect(voiceService.getVoices).toHaveBeenCalledTimes(1);
  });

  it('stops audio and closes the active dialog when destroyed', async () => {
    voiceService.getVoicePreviewStreamResponse.and.resolveTo(
      responseWithChunks(),
    );
    await component.previewVoice('voice-id');
    component.openCreateVoiceDialog();

    component.ngOnDestroy();

    expect(player.stop).toHaveBeenCalledTimes(1);
    expect(dialogRef.close).toHaveBeenCalledTimes(1);
  });
});
