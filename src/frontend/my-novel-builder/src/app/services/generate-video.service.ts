import { HttpEvent } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { VideoGenerationRequestDto } from '../types/dtos/generate/video-generation-request.dto';
import { VideoGenerationModelInfoDto } from '../types/dtos/generate/video-generation-model-info.dto';

@Injectable()
export abstract class GenerateVideoService {
  abstract generateVideo(
    request: VideoGenerationRequestDto,
  ): Observable<HttpEvent<Blob>>;
  abstract generateVideoFromImage(
    image: File,
    request: VideoGenerationRequestDto,
  ): Observable<HttpEvent<Blob>>;
  abstract getAvailableModels(): Observable<VideoGenerationModelInfoDto[]>;
}
