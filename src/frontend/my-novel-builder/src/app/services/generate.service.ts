import { HttpClient, HttpEvent } from '@angular/common/http';
import { Observable, map } from 'rxjs';
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

  // These basically never change, so we can cache them through the lifetime of the app
  cachedModels: string[] | null = null;

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

  getAvailableModels(): Observable<string[]> {
    if (this.cachedModels !== null) {
      return new Observable((observer) => {
        observer.next(this.cachedModels!);
        observer.complete();
      });
    }

    return this.mocked
      ? new Observable((observer) => {
          observer.next(['mocked-model']);
          observer.complete();
        })
      : this.http.get<any>('https://openrouter.ai/api/v1/models').pipe(
          map((response) => {
            const models = response.data.map((model: any) => model.id);

            // Sort the models alphabetically
            models.sort();

            this.cachedModels = models;
            return models;
          })
        );
  }
}
