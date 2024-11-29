import { HttpClient, HttpEvent } from '@angular/common/http';
import { environment } from '../../environment';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ImageGenRequestDto } from '../types/dtos/generate/image-gen-request.dto';

@Injectable({
  providedIn: 'root',
})
export class GenerateImageService {
  private baseUrl = environment.api.baseUrl;
  private mocked = environment.mocked;

  constructor(private http: HttpClient) {}

  generateImage(request: ImageGenRequestDto): Observable<HttpEvent<Blob>> {
    return this.http.post(`${this.baseUrl}/generate/image`, request, {
          observe: 'events',
          reportProgress: true,
          responseType: 'blob',
        });
  }
}
