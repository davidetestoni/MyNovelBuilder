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
    // Add the model to the start of the recently used models list
    this.saveRecentlyUsedModel(request.model);

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
        observer.next(this.sortModels(this.cachedModels!));
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
            models.sort();
            this.cachedModels = models;
            return this.sortModels(models);
          })
        );
  }

  private saveRecentlyUsedModel(model: string): void {
    // Save to local storage
    const recentlyUsedModels = this.getRecentlyUsedModels();

    // If the model is already in the list, remove it
    const index = recentlyUsedModels.indexOf(model);
    if (index !== -1) {
      recentlyUsedModels.splice(index, 1);
    }

    // Add the model to the start of the list
    recentlyUsedModels.unshift(model);

    // Keep only the last 10 models
    if (recentlyUsedModels.length > 10) {
      recentlyUsedModels.pop();
    }

    localStorage.setItem(
      'recentlyUsedModels',
      JSON.stringify(recentlyUsedModels)
    );
  }

  getRecentlyUsedModels(): string[] {
    // Get from local storage
    const models = localStorage.getItem('recentlyUsedModels');

    if (!models) {
      return [];
    }

    return JSON.parse(models);
  }

  sortModels(models: string[]): string[] {
    // Push the recently used models to the start of the list
    const recentlyUsedModels = this.getRecentlyUsedModels();

    // Remove the recently used models from the list
    models = models.filter((model) => !recentlyUsedModels.includes(model));

    // Add the recently used models to the start of the list
    models.unshift(...recentlyUsedModels);

    return models;
  }
}
