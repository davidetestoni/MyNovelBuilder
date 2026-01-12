import { HttpClient, HttpEvent } from '@angular/common/http';
import { environment } from '../../environment';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ImageGenRequestDto } from '../types/dtos/generate/image-gen-request.dto';
import { mockedImageGenerationResponse } from './mock';

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
}
