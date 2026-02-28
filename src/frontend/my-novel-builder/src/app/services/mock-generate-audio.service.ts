import { HttpEvent, HttpResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { TtsRequestDto } from '../types/dtos/generate/tts-request.dto';
import { TtsModelDto } from '../types/dtos/generate/tts-model.dto';
import { TtsProvider } from '../types/enums/tts-provider';
import { mockedAvailableModels } from './mocks/mock-generate-audio.data';
import { mockObservable } from './mocks/mock-utils';
import { GenerateAudioService } from './generate-audio.service';

@Injectable()
export class MockGenerateAudioService extends GenerateAudioService {
  textToSpeech(_request: TtsRequestDto): Observable<HttpEvent<Blob>> {
    return mockObservable(
      new HttpResponse({
        body: new Blob(['mock-audio-data'], { type: 'audio/wav' }),
        status: 200,
        statusText: 'OK',
      }),
    );
  }

  textToSpeechStreamResponse(_request: TtsRequestDto): Promise<Response> {
    return Promise.resolve(
      new Response(new Blob(['mock-audio-data'], { type: 'audio/wav' }), {
        status: 200,
      }),
    );
  }

  getAvailableModels(_ttsProvider: TtsProvider | null): Observable<TtsModelDto[]> {
    return mockObservable(mockedAvailableModels);
  }

  getBalanceUsd(_provider: TtsProvider): Observable<number | null> {
    return mockObservable(8.76);
  }
}
