import { HttpClient, HttpEvent } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environment';
import { ImageGenRequestDto } from '../types/dtos/generate/image-gen-request.dto';
import { ImageGenerationModelInfoDto } from '../types/dtos/generate/image-generation-model-info.dto';
import { GenerateImageService } from './generate-image.service';

@Injectable()
export class ApiGenerateImageService extends GenerateImageService {
  private http = inject(HttpClient);

  private baseUrl = environment.api.baseUrl;

  generateImage(request: ImageGenRequestDto): Observable<HttpEvent<Blob>> {
    return this.http.post(`${this.baseUrl}/generate/image`, request, {
      observe: 'events',
      reportProgress: true,
      responseType: 'blob' as const,
    });
  }

  editImage(image: File, request: ImageGenRequestDto): Observable<HttpEvent<Blob>> {
    const formData = new FormData();
    formData.append('image', image);
    formData.append('modelId', request.modelId);
    formData.append('prompt', request.prompt);
    formData.append('width', request.width.toString());
    formData.append('height', request.height.toString());

    return this.http.post(`${this.baseUrl}/generate/image/edit`, formData, {
      observe: 'events',
      reportProgress: true,
      responseType: 'blob' as const,
    });
  }

  getAvailableModels(): Observable<ImageGenerationModelInfoDto[]> {
    return this.http.get<ImageGenerationModelInfoDto[]>(`${this.baseUrl}/generate/image/models`);
  }
}
