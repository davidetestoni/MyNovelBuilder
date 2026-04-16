import { HttpEvent } from '@angular/common/http';
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

@Injectable()
export abstract class GenerateAudioService {
  abstract textToSpeech(request: TtsRequestDto): Observable<HttpEvent<Blob>>;
  abstract textToSpeechStreamResponse(request: TtsRequestDto): Promise<Response>;
  abstract immersiveTextToSpeechStreamResponse(
    request: ImmersiveTtsRequestDto,
  ): Promise<Response>;
  abstract getImmersiveTextToSpeechDebug(
    request: ImmersiveTtsRequestDto,
  ): Observable<ImmersiveTtsDebugResponseDto>;
  abstract getAvailableModels(ttsProvider: TtsProvider | null): Observable<TtsModelDto[]>;
  abstract getAvailableProviders(): Observable<TtsProviderDto[]>;
  abstract voiceDesign(request: VoiceDesignRequestDto): Observable<Blob>;
  abstract getBalanceUsd(provider: TtsProvider): Observable<number | null>;
}
