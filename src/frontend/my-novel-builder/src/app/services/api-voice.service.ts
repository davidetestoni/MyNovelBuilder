import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environment';
import { VoiceDto } from '../types/dtos/voice/voice.dto';
import { VoiceGender } from '../types/enums/voice-gender';
import { WritingLanguage } from '../types/enums/writing-language';
import { VoiceService } from './voice.service';

@Injectable()
export class ApiVoiceService extends VoiceService {
  private http = inject(HttpClient);

  private baseUrl = environment.api.baseUrl;

  getVoices(): Observable<VoiceDto[]> {
    return this.http.get<VoiceDto[]>(`${this.baseUrl}/voices`);
  }
  
  createVoice(
    name: string,
    voiceGender: VoiceGender,
    language: WritingLanguage,
    file: File,
  ): Observable<VoiceDto> {
    const formData = new FormData();
    formData.append('name', name);
    formData.append('voiceGender', voiceGender);
    formData.append('language', language);
    formData.append('file', file);
    return this.http.post<VoiceDto>(`${this.baseUrl}/voices`, formData);
  }
  
  updateVoice(
    id: string,
    name: string,
    voiceGender: VoiceGender,
    language: WritingLanguage,
    file: File,
  ): Observable<VoiceDto> {
    const formData = new FormData();
    formData.append('id', id);
    formData.append('name', name);
    formData.append('voiceGender', voiceGender);
    formData.append('language', language);
    formData.append('file', file);
    return this.http.put<VoiceDto>(`${this.baseUrl}/voices`, formData);
  }
  
  deleteVoice(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/voices/${id}`);
  }

  getVoicePreviewStreamResponse(voiceId: string, seconds: number): Promise<Response> {
    const previewUrl = `${this.baseUrl}/voices/${voiceId}/preview?seconds=${seconds}`;

    return fetch(previewUrl).then((response) => {
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      return response;
    });
  }
}
