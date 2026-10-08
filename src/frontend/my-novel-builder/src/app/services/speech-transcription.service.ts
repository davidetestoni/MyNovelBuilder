import { Injectable } from '@angular/core';
import { WritingLanguage } from '../types/enums/writing-language';

const languageCodes: Partial<Record<WritingLanguage, string>> = {
  [WritingLanguage.English]: 'en',
  [WritingLanguage.German]: 'de',
  [WritingLanguage.French]: 'fr',
  [WritingLanguage.Spanish]: 'es',
  [WritingLanguage.Italian]: 'it',
};

/** Browser transcription kept independent of the voice editor and TTS providers. */
@Injectable({ providedIn: 'root' })
export class SpeechTranscriptionService {
  supportsLanguage(language: WritingLanguage): boolean {
    return languageCodes[language] !== undefined;
  }

  async transcribe(
    audio: Blob,
    language: WritingLanguage,
    signal: AbortSignal,
    onStatus: (status: string) => void = () => {},
  ): Promise<string> {
    const languageCode = languageCodes[language];
    if (!languageCode) throw new Error('Browser transcription does not support the selected language.');
    signal.throwIfAborted();
    onStatus('Preparing audio…');

    // Decode any supported WAV encoding, mix all channels, and resample for Whistle.
    const decoder = new OfflineAudioContext(1, 1, 16000);
    let decoded: AudioBuffer;
    try {
      decoded = await decoder.decodeAudioData(await audio.arrayBuffer());
    } catch {
      throw new Error('Could not read this WAV sample. Please select a valid audio file.');
    }
    signal.throwIfAborted();
    if (decoded.duration <= 0 || decoded.duration > 30) {
      throw new Error('Browser transcription requires a voice sample of 30 seconds or less.');
    }

    const renderer = new OfflineAudioContext(1, Math.ceil(decoded.duration * 16000), 16000);
    const mono = renderer.createBuffer(1, decoded.length, decoded.sampleRate);
    const mixed = mono.getChannelData(0);
    for (let channel = 0; channel < decoded.numberOfChannels; channel++) {
      const samples = decoded.getChannelData(channel);
      for (let index = 0; index < mixed.length; index++) {
        mixed[index] += samples[index] / decoded.numberOfChannels;
      }
    }
    const source = renderer.createBufferSource();
    source.buffer = mono;
    source.connect(renderer.destination);
    source.start();
    const resampled = await renderer.startRendering();
    signal.throwIfAborted();
    const samples = resampled.getChannelData(0);

    return new Promise<string>((resolve, reject) => {
      const worker = new Worker(new URL('whistle/transcription.worker.js', document.baseURI));
      const cleanup = () => {
        signal.removeEventListener('abort', cancel);
        worker.terminate();
      };
      const cancel = () => {
        cleanup();
        reject(new DOMException('Transcription cancelled.', 'AbortError'));
      };
      signal.addEventListener('abort', cancel, { once: true });
      worker.onmessage = ({ data }: MessageEvent<{ status?: string; text?: string; error?: string }>) => {
        if (data.status) {
          onStatus(data.status);
        } else {
          cleanup();
          if (data.error) reject(new Error(data.error));
          else if (typeof data.text === 'string') resolve(data.text);
          else reject(new Error('The transcription engine returned an invalid result.'));
        }
      };
      worker.onerror = (event) => {
        event.preventDefault();
        cleanup();
        reject(new Error('Could not run browser transcription. Please try another browser.'));
      };
      worker.postMessage({ samples, language: languageCode }, [samples.buffer]);
    });
  }
}
