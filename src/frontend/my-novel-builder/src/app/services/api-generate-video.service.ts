import { HttpClient, HttpEvent } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environment';
import { VideoGenerationRequestDto } from '../types/dtos/generate/video-generation-request.dto';
import { VideoGenerationModelInfoDto } from '../types/dtos/generate/video-generation-model-info.dto';
import { GenerateVideoService } from './generate-video.service';

@Injectable()
export class ApiGenerateVideoService extends GenerateVideoService {
  private http = inject(HttpClient);

  private baseUrl = environment.api.baseUrl;

  generateVideo(request: VideoGenerationRequestDto): Observable<HttpEvent<Blob>> {
    return this.http.post(`${this.baseUrl}/generate/video`, request, {
      observe: 'events',
      reportProgress: true,
      responseType: 'blob' as const,
    });
  }

  generateVideoFromImage(
    image: File,
    request: VideoGenerationRequestDto,
  ): Observable<HttpEvent<Blob>> {
    const formData = new FormData();
    formData.append('image', image);
    formData.append('modelId', request.modelId);
    formData.append('prompt', request.prompt);
    formData.append('width', request.width.toString());
    formData.append('height', request.height.toString());

    return this.http.post(`${this.baseUrl}/generate/video/from-image`, formData, {
      observe: 'events',
      reportProgress: true,
      responseType: 'blob' as const,
    });
  }

  getAvailableModels(): Observable<VideoGenerationModelInfoDto[]> {
    return this.http.get<VideoGenerationModelInfoDto[]>(
      `${this.baseUrl}/generate/video/models`,
    );
  }
}
