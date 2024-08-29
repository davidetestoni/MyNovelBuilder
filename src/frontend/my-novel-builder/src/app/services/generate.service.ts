import { HttpClient, HttpEvent } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../environment';
import { mockedTextGenerationResponse } from './mock';
import { Injectable } from '@angular/core';
import { GenerateTextRequestDto } from '../types/dtos/generate/generate-text-request.dto';
import { Moment } from 'moment';
import moment from 'moment';

@Injectable({
  providedIn: 'root',
})
export class GenerateService {
  private baseUrl = environment.api.baseUrl;
  private mocked = environment.mocked;

  // These rarely change, so we can cache them for 6 hours
  cachedModels: string[] | null = null;
  cachedModelsKey = 'cachedModels';
  cachedModelsExpiry = 6 * 60 * 60 * 1000; // 6 hours in milliseconds
  cachedModelsLastUpdated: Moment | null = null;
  cachedModelsLastUpdatedKey = 'cachedModelsLastUpdated';

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
    // If we have cached models and they are not expired, return them
    if (
      this.cachedModels !== null &&
      this.cachedModelsLastUpdated !== null &&
      moment().diff(this.cachedModelsLastUpdated) < this.cachedModelsExpiry
    ) {
      console.log('Returning cached models from memory');
      return new Observable((observer) => {
        observer.next(this.sortModels(this.cachedModels!));
        observer.complete();
      });
    }

    // Otherwise, get them from local storage
    const cachedModels = localStorage.getItem(this.cachedModelsKey);
    const cachedModelsLastUpdated = localStorage.getItem(
      this.cachedModelsLastUpdatedKey
    );
    this.cachedModels = cachedModels
      ? this.getModelIds(JSON.parse(cachedModels))
      : null;
    this.cachedModelsLastUpdated = cachedModelsLastUpdated
      ? moment(cachedModelsLastUpdated)
      : null;

    // If we have cached models and they are not expired, return them
    if (
      this.cachedModels !== null &&
      this.cachedModelsLastUpdated !== null &&
      moment().diff(this.cachedModelsLastUpdated) < this.cachedModelsExpiry
    ) {
      console.log('Returning cached models from local storage');
      return new Observable((observer) => {
        observer.next(this.sortModels(this.cachedModels!));
        observer.complete();
      });
    }

    // Otherwise, fetch them from the API
    console.log('Fetching models from the API');
    return this.mocked
      ? new Observable((observer) => {
          observer.next(['mocked-model']);
          observer.complete();
        })
      : this.http.get<any>('https://openrouter.ai/api/v1/models').pipe(
          map((response) => {
            this.cachedModels = this.getModelIds(response);
            this.cachedModelsLastUpdated = moment();

            // Save to local storage
            localStorage.setItem(
              this.cachedModelsKey,
              JSON.stringify(response)
            );
            localStorage.setItem(
              this.cachedModelsLastUpdatedKey,
              this.cachedModelsLastUpdated.toISOString()
            );

            return this.sortModels(this.cachedModels);
          })
        );
  }

  private getModelIds(response: any): string[] {
    return response.data.map((model: any) => model.id);
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
