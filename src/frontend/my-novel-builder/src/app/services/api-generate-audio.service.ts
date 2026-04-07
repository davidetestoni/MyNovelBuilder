import { HttpClient, HttpEvent } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environment';
import { TtsRequestDto } from '../types/dtos/generate/tts-request.dto';
import { TtsModelDto } from '../types/dtos/generate/tts-model.dto';
import { TtsProviderDto } from '../types/dtos/generate/tts-provider.dto';
import { VoiceDesignRequestDto } from '../types/dtos/generate/voice-design-request.dto';
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

  getAvailableModels(ttsProvider: TtsProvider | null): Observable<TtsModelDto[]> {
    return this.http.get<TtsModelDto[]>(`${this.baseUrl}/generate/audio/tts/models?provider=${ttsProvider}`);
  }

  getAvailableProviders(): Observable<TtsProviderDto[]> {
    return this.http.get<TtsProviderDto[]>(`${this.baseUrl}/generate/audio/tts/providers`);
  }

  voiceDesign(request: VoiceDesignRequestDto): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/generate/audio/tts/voice-design`, request, {
      responseType: 'blob',
    });
  }

  getBalanceUsd(provider: TtsProvider): Observable<number | null> {
    return this.http.get<number | null>(
      `${this.baseUrl}/generate/audio/balance-usd?provider=${provider}`,
    );
  }
}
