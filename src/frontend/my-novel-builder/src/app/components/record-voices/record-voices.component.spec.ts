import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { GenerateAudioService } from '../../services/generate-audio.service';
import { STREAMING_WAV_PLAYER_FACTORY } from '../../utils/streaming-wav-player.factory';
import { getVoicePreviewText } from '../../utils/tts-preview.utils';
import { TtsProvider } from '../../types/enums/tts-provider';
import { WritingLanguage } from '../../types/enums/writing-language';
import { CharacterVoiceAssignmentDto } from '../../types/dtos/compendium-record/character-voice-assignment.dto';
import { RecordVoicesComponent } from './record-voices.component';

describe('RecordVoicesComponent', () => {
  let fixture: ComponentFixture<RecordVoicesComponent>;
  let component: RecordVoicesComponent;
  let audio: jasmine.SpyObj<GenerateAudioService>;
  let player: jasmine.SpyObj<{ addChunk(chunk: Uint8Array): void; stop(): void }>;
  const assignment: CharacterVoiceAssignmentDto = {
    provider: TtsProvider.ElevenLabs, modelId: 'model', voiceId: 'voice',
    voiceName: 'Mira', gainDb: -3, updatedAt: '2026-01-01',
  };
  const chunk = new Uint8Array([1, 2, 3]);
  const sample = () => new Response(new ReadableStream<Uint8Array>({
    start(controller) { controller.enqueue(chunk); controller.close(); },
  }));

  beforeEach(async () => {
    audio = jasmine.createSpyObj('GenerateAudioService', ['getAvailableModels', 'textToSpeechStreamResponse']);
    player = jasmine.createSpyObj('player', ['addChunk', 'stop']);
    audio.getAvailableModels.and.returnValue(of([{
      modelId: 'model', name: 'Model', supportsTextEmphasis: false,
      voices: [{ voiceId: 'voice', name: 'Mira', language: WritingLanguage.Italian, previewUrl: null }],
    }]));
    audio.textToSpeechStreamResponse.and.callFake(async () => sample());
    await TestBed.configureTestingModule({
      imports: [RecordVoicesComponent],
      providers: [
        { provide: GenerateAudioService, useValue: audio },
        { provide: STREAMING_WAV_PLAYER_FACTORY, useValue: () => player },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(RecordVoicesComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('assignments', [assignment]);
    fixture.detectChanges();
  });

  it('displays read-only assignments and starts a preview on click', async () => {
    const element: HTMLElement = fixture.nativeElement;
    expect(element.textContent).toContain('Mira');
    expect(element.textContent).toContain('model');
    expect(element.textContent).toContain('-3 dB');
    expect(element.querySelectorAll('input, select, textarea').length).toBe(0);
    const preview = spyOn(component, 'preview').and.callThrough();
    element.querySelector('button')!.click();
    await preview.calls.mostRecent().returnValue;
    expect(audio.textToSpeechStreamResponse).toHaveBeenCalledWith({
      provider: assignment.provider, modelId: 'model', voiceId: 'voice', gainDb: -3,
      message: getVoicePreviewText(WritingLanguage.Italian),
    }, jasmine.any(AbortSignal));
    expect(player.addChunk).toHaveBeenCalledWith(chunk);
  });

  it('stops the active preview when clicked again', async () => {
    await component.preview(assignment);
    await component.preview(assignment);
    expect(player.stop).toHaveBeenCalled();
    expect(component.activeAssignment).toBeNull();
    expect(audio.textToSpeechStreamResponse).toHaveBeenCalledTimes(1);
  });

  it('falls back to an English sample if the catalogue is unavailable', async () => {
    audio.getAvailableModels.and.returnValue(throwError(() => new Error('offline')));
    await component.preview({ ...assignment, gainDb: null });
    expect(audio.textToSpeechStreamResponse.calls.mostRecent().args[0]).toEqual({
      provider: assignment.provider, modelId: 'model', voiceId: 'voice', gainDb: 0,
      message: getVoicePreviewText(WritingLanguage.English),
    });
  });

  it('stops playback and hides the section when assignments are cleared', async () => {
    await component.preview(assignment);
    const signal = audio.textToSpeechStreamResponse.calls.mostRecent().args[1]!;
    fixture.componentRef.setInput('assignments', []);
    fixture.detectChanges();
    expect(signal.aborted).toBeTrue();
    expect(player.stop).toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('section')).toBeNull();
  });

  it('cancels a late response when destroyed without starting playback', async () => {
    let resolve!: (response: Response) => void;
    audio.textToSpeechStreamResponse.and.returnValue(new Promise<Response>(done => resolve = done));
    const preview = component.preview(assignment);
    await Promise.resolve();
    await Promise.resolve();
    component.ngOnDestroy();
    resolve(sample());
    await preview;
    expect(player.addChunk).not.toHaveBeenCalled();
    expect(component.activeAssignment).toBeNull();
  });

  it('reports preview failures and allows retrying', async () => {
    audio.textToSpeechStreamResponse.and.rejectWith(new Error('offline'));
    await component.preview(assignment);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('Could not preview voice');
    expect(component.loading).toBeFalse();
    audio.textToSpeechStreamResponse.and.callFake(async () => sample());
    await component.preview(assignment);
    expect(component.error).toBeNull();
    expect(player.addChunk).toHaveBeenCalledWith(chunk);
  });
});
