import { HttpEvent } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { VideoGenerationRequestDto } from '../types/dtos/generate/video-generation-request.dto';
import { VideoGenerationModelInfoDto } from '../types/dtos/generate/video-generation-model-info.dto';
import {
  mockedVideoGenerationResponse,
  mockedVideoModelInfos,
} from './mocks/mock-generate-video.data';
import { mockObservable } from './mocks/mock-utils';
import { GenerateVideoService } from './generate-video.service';

@Injectable()
export class MockGenerateVideoService extends GenerateVideoService {
  generateVideo(
    _request: VideoGenerationRequestDto,
  ): Observable<HttpEvent<Blob>> {
    return mockedVideoGenerationResponse();
  }

  generateVideoFromImage(
    _image: File,
    _request: VideoGenerationRequestDto,
  ): Observable<HttpEvent<Blob>> {
    return mockedVideoGenerationResponse();
  }

  getAvailableModels(): Observable<VideoGenerationModelInfoDto[]> {
    return mockObservable<VideoGenerationModelInfoDto[]>(mockedVideoModelInfos);
  }
}
