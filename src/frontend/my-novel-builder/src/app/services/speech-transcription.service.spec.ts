import { SpeechTranscriptionService } from './speech-transcription.service';
import { WritingLanguage } from '../types/enums/writing-language';

function silentWav(seconds: number, channels = 1, sampleRate = 16000): Blob {
  const frames = seconds * sampleRate;
  const bytes = new ArrayBuffer(44 + frames * channels * 2);
  const view = new DataView(bytes);
  const text = (offset: number, value: string) => {
    for (let index = 0; index < value.length; index++) view.setUint8(offset + index, value.charCodeAt(index));
  };
  text(0, 'RIFF'); view.setUint32(4, bytes.byteLength - 8, true);
  text(8, 'WAVE'); text(12, 'fmt '); view.setUint32(16, 16, true);
  view.setUint16(20, 1, true); view.setUint16(22, channels, true);
  view.setUint32(24, sampleRate, true); view.setUint32(28, sampleRate * channels * 2, true);
  view.setUint16(32, channels * 2, true); view.setUint16(34, 16, true);
  text(36, 'data'); view.setUint32(40, bytes.byteLength - 44, true);
  return new Blob([bytes], { type: 'audio/wav' });
}

describe('SpeechTranscriptionService', () => {
  let service: SpeechTranscriptionService;
  beforeEach(() => { service = new SpeechTranscriptionService(); });

  it('does not load any Whistle assets when the service is created or language support is checked', () => {
    const worker = spyOn(window, 'Worker');
    const fetchAssets = spyOn(window, 'fetch');
    const idleService = new SpeechTranscriptionService();
    idleService.supportsLanguage(WritingLanguage.English);
    expect(worker).not.toHaveBeenCalled();
    expect(fetchAssets).not.toHaveBeenCalled();
  });

  it('reports supported app languages and rejects Russian without starting a worker', async () => {
    expect(service.supportsLanguage(WritingLanguage.Italian)).toBeTrue();
    expect(service.supportsLanguage(WritingLanguage.English)).toBeTrue();
    expect(service.supportsLanguage(WritingLanguage.Russian)).toBeFalse();
    const worker = spyOn(window, 'Worker');
    await expectAsync(service.transcribe(silentWav(1), WritingLanguage.Russian, new AbortController().signal))
      .toBeRejectedWithError('Browser transcription does not support the selected language.');
    expect(worker).not.toHaveBeenCalled();
  });

  it('rejects clips over 30 seconds without truncating or loading the model', async () => {
    const worker = spyOn(window, 'Worker');
    await expectAsync(service.transcribe(silentWav(31), WritingLanguage.English, new AbortController().signal))
      .toBeRejectedWithError('Browser transcription requires a voice sample of 30 seconds or less.');
    expect(worker).not.toHaveBeenCalled();
  });

  it('rejects invalid audio without starting a worker', async () => {
    const worker = spyOn(window, 'Worker');
    await expectAsync(service.transcribe(new Blob(['invalid']), WritingLanguage.English, new AbortController().signal))
      .toBeRejectedWithError('Could not read this WAV sample. Please select a valid audio file.');
    expect(worker).not.toHaveBeenCalled();
  });

  it('honors cancellation before decoding', async () => {
    const controller = new AbortController();
    controller.abort();
    const worker = spyOn(window, 'Worker');
    await expectAsync(service.transcribe(silentWav(1), WritingLanguage.English, controller.signal)).toBeRejected();
    expect(worker).not.toHaveBeenCalled();
  });

  it('resamples stereo WAVs and terminates the worker after receiving a result', async () => {
    const fake = {
      onmessage: null as ((event: MessageEvent) => void) | null,
      onerror: null,
      terminate: jasmine.createSpy('terminate'),
      postMessage: jasmine.createSpy('postMessage').and.callFake((data: { samples: Float32Array; language: string }) => {
        expect(data.samples.length).toBe(16000);
        expect(data.language).toBe('it');
        fake.onmessage!(new MessageEvent('message', { data: { text: 'Test transcript.' } }));
      }),
    };
    spyOn(window, 'Worker').and.returnValue(fake as unknown as Worker);
    const result = await service.transcribe(silentWav(1, 2, 44100), WritingLanguage.Italian, new AbortController().signal);
    expect(result).toBe('Test transcript.');
    expect(fake.terminate).toHaveBeenCalledTimes(1);
  });

  it('terminates an active worker when cancelled', async () => {
    const controller = new AbortController();
    const fake = {
      onmessage: null,
      onerror: null,
      terminate: jasmine.createSpy('terminate'),
      postMessage: () => controller.abort(),
    };
    spyOn(window, 'Worker').and.returnValue(fake as unknown as Worker);
    await expectAsync(service.transcribe(silentWav(1), WritingLanguage.English, controller.signal)).toBeRejected();
    expect(fake.terminate).toHaveBeenCalledTimes(1);
  });

  it('loads the real bundled Whistle worker and returns an empty transcript for silence', async () => {
    const status = jasmine.createSpy('status');
    const result = await service.transcribe(silentWav(1), WritingLanguage.English, new AbortController().signal, status);
    expect(result).toBe('');
    expect(status).toHaveBeenCalledWith('Loading transcription model…');
    expect(status).toHaveBeenCalledWith('Transcribing…');
  }, 30000);
});
