import { HttpClient, HttpEvent } from '@angular/common/http';
import { environment } from '../../environment';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ImageGenRequestDto } from '../types/dtos/generate/image-gen-request.dto';
import { mockedImageGenerationResponse, mockObservable } from './mock';
import { ImageGenerationModelInfoDto } from '../types/dtos/generate/image-generation-model-info.dto';

@Injectable({
  providedIn: 'root',
})
export class GenerateImageService {
  private http = inject(HttpClient);

  private baseUrl = environment.api.baseUrl;
  private mocked = environment.mocked;

  generateImage(request: ImageGenRequestDto): Observable<HttpEvent<Blob>> {
    return this.mocked
      ? mockedImageGenerationResponse()
      : this.http.post(`${this.baseUrl}/generate/image`, request, {
          observe: 'events',
          reportProgress: true,
          responseType: 'blob',
        });
  }

  getAvailableModels(): Observable<ImageGenerationModelInfoDto[]> {
    return this.mocked
      ? mockObservable<ImageGenerationModelInfoDto[]>([
          { modelId: 'z-image/turbo', name: 'Z-Image Turbo' },
          { modelId: 'z-image/hd', name: 'Z-Image HD' },
        ])
      : this.http.get<ImageGenerationModelInfoDto[]>(
          `${this.baseUrl}/generate/image/models`,
        );
  }
}
