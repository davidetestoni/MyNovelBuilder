import { HttpEvent } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { TtsRequestDto } from '../types/dtos/generate/tts-request.dto';
import { TtsModelDto } from '../types/dtos/generate/tts-model.dto';
import { TtsProvider } from '../types/enums/tts-provider';

@Injectable()
export abstract class GenerateAudioService {
  abstract textToSpeech(request: TtsRequestDto): Observable<HttpEvent<Blob>>;
  abstract textToSpeechStreamResponse(request: TtsRequestDto): Promise<Response>;
  abstract getAvailableModels(ttsProvider: TtsProvider | null): Observable<TtsModelDto[]>;
  abstract getBalanceUsd(provider: TtsProvider): Observable<number | null>;
}
