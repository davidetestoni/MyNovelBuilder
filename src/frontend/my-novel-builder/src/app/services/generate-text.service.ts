import { HttpClient, HttpEvent, HttpEventType, HttpResponse } from '@angular/common/http';
import { Observable, map, filter } from 'rxjs';
import { environment } from '../../environment';
import { mockedTextGenerationResponse } from './mock';
import { Injectable, inject } from '@angular/core';
import { GenerateTextRequestDto } from '../types/dtos/generate/generate-text-request.dto';
import { TextGenerationModelInfoDto } from '../types/dtos/generate/text-generation-model-info.dto';
import { DescribeImageRequestDto } from '../types/dtos/generate/describe-image-request.dto';
import { GenerateTextResponseChunkDto } from '../types/dtos/generate/generate-text-response-chunk.dto';

export interface GenerateTextCompletion {
  content: string;
  rawResponse: string;
  parseError: string | null;
}

@Injectable({
  providedIn: 'root',
})
export class GenerateTextService {
  private http = inject(HttpClient);

  private baseUrl = environment.api.baseUrl;
  private mocked = environment.mocked;

  generateText(
    request: GenerateTextRequestDto,
  ): Observable<HttpEvent<string>> {
    // Add the model to the start of the recently used models list
    this.saveRecentlyUsedModel(request.model);

    return this.mocked
      ? mockedTextGenerationResponse('This is a generated text response')
      : this.http.post(`${this.baseUrl}/generate/text/streamed`, request, {
          observe: 'events',
          reportProgress: true,
          responseType: 'text' as const,
        });
  }

  generateTextCompletion(
    request: GenerateTextRequestDto,
  ): Observable<GenerateTextCompletion> {
    this.saveRecentlyUsedModel(request.model);

    if (this.mocked) {
      return new Observable<GenerateTextCompletion>((observer) => {
        const mockedContent = JSON.stringify([
          {
            title: 'Mocked Story Event',
            date: 'Day 1',
            description: 'A mocked story event description.',
          },
        ]);
        observer.next({
          content: mockedContent,
          rawResponse: mockedContent,
          parseError: null,
        });
        observer.complete();
      });
    }

    return this.http
      .post(`${this.baseUrl}/generate/text/streamed`, request, {
        observe: 'events',
        reportProgress: true,
        responseType: 'text' as const,
      })
      .pipe(
        filter((event) => event.type === HttpEventType.Response),
        map((event) => {
          const response = event as HttpResponse<string>;
          const rawResponse = response.body ?? '';
          try {
            const responseChunks = rawResponse
              .split('\n')
              .filter((item) => item.length > 0)
              .map((item) => JSON.parse(item) as GenerateTextResponseChunkDto);
            const content = responseChunks.map((item) => item.content).join('');
            return { content, rawResponse, parseError: null };
          } catch (error) {
            return {
              content: '',
              rawResponse,
              parseError:
                error instanceof Error ? error.message : 'Unable to parse response',
            };
          }
        }),
      );
  }

  describeImage(image: Blob, request: DescribeImageRequestDto): Observable<string> {
    this.saveRecentlyUsedModel(request.model);

    if (this.mocked) {
      return new Observable<string>((observer) => {
        observer.next(
          'A generated image description. This is mocked data from the frontend service.',
        );
        observer.complete();
      });
    }

    const formData = new FormData();
    formData.append('image', image, 'image.png');
    formData.append('model', request.model);
    formData.append('promptId', request.promptId);
    formData.append('compendiumId', request.compendiumId);

    if (request.instructions !== null && request.instructions.trim() !== '') {
      formData.append('instructions', request.instructions);
    }

    return this.http.post(
      `${this.baseUrl}/generate/text/describe-image`,
      formData,
      {
        responseType: 'text' as const,
      },
    );
  }

  getAvailableModels(): Observable<string[]> {
    return this.getAvailableModelInfos().pipe(
      map((models) => {
        return this.sortModels(models.map((model) => model.id));
      }),
    );
  }

  getAvailableVisionModels(): Observable<string[]> {
    return this.getAvailableModelInfos().pipe(
      map((models) =>
        this.sortModels(
          models
            .filter((model) => model.isVisionCapable ?? false)
            .map((model) => model.id),
        ),
      ),
    );
  }

  getAvailableStructuredOutputModels(): Observable<string[]> {
    return this.getAvailableModelInfos().pipe(
      map((models) =>
        this.sortModels(
          models
            .filter((model) => model.supportsStructuredOutputs ?? false)
            .map((model) => model.id),
        ),
      ),
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
      JSON.stringify(recentlyUsedModels),
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
    // Copy the array and sort it alphabetically
    models = [...models].sort();

    // Push the recently used models to the start of the list,
    // as long as they are present in the available models list
    const recentlyUsedModels = this.getRecentlyUsedModels().filter((model) =>
      models.includes(model),
    );

    // Remove the recently used models from the list
    models = models.filter((model) => !recentlyUsedModels.includes(model));

    // Add the recently used models to the start of the list
    models.unshift(...recentlyUsedModels);

    return models;
  }

  private getAvailableModelInfos(): Observable<TextGenerationModelInfoDto[]> {
    return this.mocked
      ? new Observable<TextGenerationModelInfoDto[]>((observer) => {
          observer.next([
            {
              id: 'mocked-model',
              isVisionCapable: true,
              supportsStructuredOutputs: true,
            },
          ]);
          observer.complete();
        })
      : this.http.get<TextGenerationModelInfoDto[]>(
          `${this.baseUrl}/generate/text/models`,
        );
  }
}
