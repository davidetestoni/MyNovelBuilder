import { HttpClient, HttpEvent } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environment';
import { TtsRequestDto } from '../types/dtos/generate/tts-request.dto';
import { TtsVoiceDto } from '../types/dtos/generate/tts-voice.dto';
import { TtsProvider } from '../types/enums/tts-provider';
import { GenerateAudioService } from './generate-audio.service';

@Injectable()
export class ApiGenerateAudioService extends GenerateAudioService {
  private http = inject(HttpClient);

  private baseUrl = environment.api.baseUrl;

  textToSpeech(request: TtsRequestDto): Observable<HttpEvent<Blob>> {
    return this.http.post(`${this.baseUrl}/generate/audio/tts`, request, {
      observe: 'events',
      reportProgress: true,
      responseType: 'blob' as const,
    });
  }

  textToSpeechStreamResponse(request: TtsRequestDto): Promise<Response> {
    return fetch(`${this.baseUrl}/generate/audio/tts/stream`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }).then((response) => {
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }
      return response;
    });
  }

  getAvailableVoices(ttsProvider: TtsProvider | null): Observable<TtsVoiceDto[]> {
    return this.http.get<TtsVoiceDto[]>(`${this.baseUrl}/generate/audio/tts/voices?provider=${ttsProvider}`);
  }
}
