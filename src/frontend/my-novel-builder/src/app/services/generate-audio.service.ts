import { HttpClient, HttpEvent } from '@angular/common/http';
import { environment } from '../../environment';
import { Injectable, inject } from '@angular/core';
import { TtsRequestDto } from '../types/dtos/generate/tts-request.dto';
import { Observable } from 'rxjs';
import { TtsVoiceDto } from '../types/dtos/generate/tts-voice.dto';
import { mockedAvailableVoices, mockObservable } from './mock';
import { TtsProvider } from '../types/enums/tts-provider';

@Injectable({
  providedIn: 'root',
})
export class GenerateAudioService {
  private http = inject(HttpClient);

  private baseUrl = environment.api.baseUrl;
  private mocked = environment.mocked;

  textToSpeech(request: TtsRequestDto): Observable<HttpEvent<Blob>> {
    return this.http.post(`${this.baseUrl}/generate/audio/tts`, request, {
      observe: 'events',
      reportProgress: true,
      responseType: 'blob',
    });
  }

  textToSpeechStreamResponse(request: TtsRequestDto): Promise<Response> {
    return fetch(`${this.baseUrl}/generate/audio/tts/stream`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }).then((response) => {
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      return response;
    });
  }

  getAvailableVoices(
    ttsProvider: TtsProvider | null,
  ): Observable<TtsVoiceDto[]> {
    return this.mocked
      ? mockObservable(mockedAvailableVoices)
      : this.http.get<TtsVoiceDto[]>(
          `${this.baseUrl}/generate/audio/tts/voices?provider=${ttsProvider}`,
        );
  }
}
