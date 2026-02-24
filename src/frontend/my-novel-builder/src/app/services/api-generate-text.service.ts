import {
  HttpClient,
  HttpEvent,
  HttpEventType,
  HttpResponse,
} from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, filter, map } from 'rxjs';
import { environment } from '../../environment';
import { DescribeImageRequestDto } from '../types/dtos/generate/describe-image-request.dto';
import { GenerateTextRequestDto } from '../types/dtos/generate/generate-text-request.dto';
import { GenerateTextResponseChunkDto } from '../types/dtos/generate/generate-text-response-chunk.dto';
import { TextGenerationModelInfoDto } from '../types/dtos/generate/text-generation-model-info.dto';
import { TextGenerationPreviewDto } from '../types/dtos/generate/text-generation-preview.dto';
import { GenerateTextCompletion, GenerateTextService } from './generate-text.service';

@Injectable()
export class ApiGenerateTextService extends GenerateTextService {
  private http = inject(HttpClient);

  private baseUrl = environment.api.baseUrl;

  generateText(request: GenerateTextRequestDto): Observable<HttpEvent<string>> {
    this.saveRecentlyUsedModel(request.model);

    return this.http.post(`${this.baseUrl}/generate/text/streamed`, request, {
      observe: 'events',
      reportProgress: true,
      responseType: 'text' as const,
    });
  }

  generateTextCompletion(request: GenerateTextRequestDto): Observable<GenerateTextCompletion> {
    this.saveRecentlyUsedModel(request.model);

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
              parseError: error instanceof Error ? error.message : 'Unable to parse response',
            };
          }
        }),
      );
  }

  getGenerationPreview(request: GenerateTextRequestDto): Observable<TextGenerationPreviewDto> {
    this.saveRecentlyUsedModel(request.model);
    return this.http.post<TextGenerationPreviewDto>(
      `${this.baseUrl}/generate/text/preview`,
      request,
    );
  }

  describeImage(image: Blob, request: DescribeImageRequestDto): Observable<string> {
    this.saveRecentlyUsedModel(request.model);

    const formData = new FormData();
    formData.append('image', image, 'image.png');
    formData.append('model', request.model);
    formData.append('promptId', request.promptId);
    formData.append('compendiumId', request.compendiumId);

    if (request.instructions !== null && request.instructions.trim() !== '') {
      formData.append('instructions', request.instructions);
    }

    return this.http.post(`${this.baseUrl}/generate/text/describe-image`, formData, {
      responseType: 'text' as const,
    });
  }

  protected fetchAvailableModelInfos(): Observable<TextGenerationModelInfoDto[]> {
    return this.http.get<TextGenerationModelInfoDto[]>(`${this.baseUrl}/generate/text/models`);
  }
}
