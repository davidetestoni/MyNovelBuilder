import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { VoiceDto } from '../types/dtos/voice/voice.dto';
import { VoiceGender } from '../types/enums/voice-gender';
import { WritingLanguage } from '../types/enums/writing-language';

@Injectable()
export abstract class VoiceService {
  abstract getVoices(): Observable<VoiceDto[]>;
  abstract createVoice(
    name: string,
    voiceGender: VoiceGender,
    language: WritingLanguage,
    transcript: string | null,
    file: File,
  ): Observable<VoiceDto>;
  abstract updateVoice(
    id: string,
    name: string,
    voiceGender: VoiceGender,
    language: WritingLanguage,
    transcript: string | null,
    file: File | null,
  ): Observable<VoiceDto>;
  abstract deleteVoice(id: string): Observable<void>;
  abstract getVoicePreviewStreamResponse(
    voiceId: string,
    seconds: number,
  ): Promise<Response>;
}
