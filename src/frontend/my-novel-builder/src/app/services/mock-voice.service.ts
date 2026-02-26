import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { VoiceDto } from '../types/dtos/voice/voice.dto';
import { VoiceGender } from '../types/enums/voice-gender';
import { WritingLanguage } from '../types/enums/writing-language';
import { mockObservable } from './mocks/mock-utils';
import { mockedVoices } from './mocks/mock-voice.data';
import { VoiceService } from './voice.service';

@Injectable()
export class MockVoiceService extends VoiceService {
  private voices: VoiceDto[] = [...mockedVoices];

  getVoices(): Observable<VoiceDto[]> {
    return mockObservable(this.voices);
  }

  createVoice(
    name: string,
    voiceGender: VoiceGender,
    language: WritingLanguage,
    _file: File,
  ): Observable<VoiceDto> {
    const now = new Date().toISOString();
    const voice: VoiceDto = {
      id: crypto.randomUUID(),
      createdAt: now,
      updatedAt: now,
      name,
      voiceGender,
      language,
    };

    this.voices = [voice, ...this.voices];
    return mockObservable(voice);
  }

  updateVoice(
    id: string,
    name: string,
    voiceGender: VoiceGender,
    language: WritingLanguage,
    _file: File,
  ): Observable<VoiceDto> {
    const current = this.voices.find((v) => v.id === id);
    const updated: VoiceDto = {
      id,
      createdAt: current?.createdAt ?? new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      name,
      voiceGender,
      language,
    };

    this.voices = this.voices.map((voice) => (voice.id === id ? updated : voice));
    return mockObservable(updated);
  }

  deleteVoice(id: string): Observable<void> {
    this.voices = this.voices.filter((voice) => voice.id !== id);
    return mockObservable<void>(undefined);
  }

  getVoicePreviewStreamResponse(_voiceId: string, seconds: number): Promise<Response> {
    const durationSeconds = Math.max(1, seconds);
    const wavBuffer = this.createSilentWav(durationSeconds);
    const blob = new Blob([wavBuffer], { type: 'audio/wav' });

    return Promise.resolve(
      new Response(blob, {
        status: 200,
        headers: {
          'Content-Type': 'audio/wav',
        },
      }),
    );
  }

  private createSilentWav(durationSeconds: number): ArrayBuffer {
    const sampleRate = 16000;
    const channelCount = 1;
    const bitsPerSample = 16;
    const bytesPerSample = bitsPerSample / 8;
    const dataLength = sampleRate * durationSeconds * channelCount * bytesPerSample;
    const totalLength = 44 + dataLength;
    const buffer = new ArrayBuffer(totalLength);
    const view = new DataView(buffer);

    // RIFF header
    this.writeAscii(view, 0, 'RIFF');
    view.setUint32(4, 36 + dataLength, true);
    this.writeAscii(view, 8, 'WAVE');

    // fmt chunk
    this.writeAscii(view, 12, 'fmt ');
    view.setUint32(16, 16, true); // PCM header length
    view.setUint16(20, 1, true); // PCM format
    view.setUint16(22, channelCount, true);
    view.setUint32(24, sampleRate, true);
    view.setUint32(28, sampleRate * channelCount * bytesPerSample, true); // byte rate
    view.setUint16(32, channelCount * bytesPerSample, true); // block align
    view.setUint16(34, bitsPerSample, true);

    // data chunk
    this.writeAscii(view, 36, 'data');
    view.setUint32(40, dataLength, true);

    return buffer;
  }

  private writeAscii(view: DataView, offset: number, value: string): void {
    for (let i = 0; i < value.length; i++) {
      view.setUint8(offset + i, value.charCodeAt(i));
    }
  }
}
