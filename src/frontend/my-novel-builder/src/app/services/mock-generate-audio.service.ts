import { HttpEvent, HttpResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { TtsRequestDto } from '../types/dtos/generate/tts-request.dto';
import { TtsModelDto } from '../types/dtos/generate/tts-model.dto';
import { TtsProviderDto } from '../types/dtos/generate/tts-provider.dto';
import { VoiceDesignRequestDto } from '../types/dtos/generate/voice-design-request.dto';
import {
  ImmersiveTtsDebugResponseDto,
  ImmersiveTtsRequestDto,
} from '../types/dtos/generate/immersive-tts-request.dto';
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

  immersiveTextToSpeechStreamResponse(
    _request: ImmersiveTtsRequestDto,
  ): Promise<Response> {
    return Promise.resolve(
      new Response(new Blob(['mock-audio-data'], { type: 'audio/wav' }), {
        status: 200,
      }),
    );
  }

  getImmersiveTextToSpeechDebug(
    _request: ImmersiveTtsRequestDto,
  ): Observable<ImmersiveTtsDebugResponseDto> {
    return mockObservable({
      provider: TtsProvider.Qwen3,
      ttsModelId: 'model-1',
      textGenerationModelId: 'model-1',
      pauseMs: 150,
      chunks: [
        {
          sequence: 0,
          speakerKind: 'narrator',
          speakerName: 'Narrator',
          characterRecordId: null,
          voiceId: 'voice-1',
          isNarratorFallback: false,
          text: 'Mock immersive chunk.',
        },
      ],
    });
  }

  getAvailableModels(_ttsProvider: TtsProvider | null): Observable<TtsModelDto[]> {
    return mockObservable(mockedAvailableModels);
  }

  getAvailableProviders(): Observable<TtsProviderDto[]> {
    return mockObservable([
      {
        provider: TtsProvider.Qwen3,
        supportsVoiceDesign: true,
      },
      {
        provider: TtsProvider.OmniVoice,
        supportsVoiceDesign: true,
      },
    ]);
  }

  voiceDesign(_request: VoiceDesignRequestDto): Observable<Blob> {
    return mockObservable(new Blob(['mock-audio-data'], { type: 'audio/wav' }));
  }

  getBalanceUsd(_provider: TtsProvider): Observable<number | null> {
    return mockObservable(8.76);
  }
}
