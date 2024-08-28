import { HttpClient, HttpEvent } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environment';
import { mockedTextGenerationResponse } from './mock';
import { Injectable } from '@angular/core';
import { GenerateTextRequestDto } from '../types/dtos/generate/generate-text-request.dto';

@Injectable({
  providedIn: 'root',
})
export class GenerateService {
  private baseUrl = environment.api.baseUrl;
  private mocked = environment.mocked;

  constructor(private http: HttpClient) {}

  generateText(request: GenerateTextRequestDto): Observable<HttpEvent<string>> {
    return this.mocked
      ? mockedTextGenerationResponse('This is a generated text response')
      : this.http.post(`${this.baseUrl}/generate/text/streamed`, request, {
          observe: 'events',
          reportProgress: true,
          responseType: 'text',
        });
  }
}
