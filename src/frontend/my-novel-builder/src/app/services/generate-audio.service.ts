import { HttpClient, HttpEvent } from '@angular/common/http';
import { environment } from '../../environment';
import { Injectable } from '@angular/core';
import { TtsRequestDto } from '../types/dtos/generate/tts-request.dto';
import { Observable } from 'rxjs';
import { TtsVoiceDto } from '../types/dtos/generate/tts-voice.dto';
import { mockedAvailableVoices, mockObservable } from './mock';

@Injectable({
  providedIn: 'root',
})
export class GenerateAudioService {
  private baseUrl = environment.api.baseUrl;
  private mocked = environment.mocked;

  constructor(private http: HttpClient) {}

  textToSpeech(request: TtsRequestDto): Observable<HttpEvent<Blob>> {
    return this.http.post(`${this.baseUrl}/generate/audio/tts`, request, {
          observe: 'events',
          reportProgress: true,
          responseType: 'blob',
        });
  }

  getAvailableVoices(): Observable<TtsVoiceDto[]> {
    return this.mocked
        ? mockObservable(mockedAvailableVoices)
        : this.http.get<TtsVoiceDto[]>(`${this.baseUrl}/generate/audio/tts/voices`);
  }
}
