import {
  HttpClient,
  HttpDownloadProgressEvent,
  HttpEvent,
  HttpEventType,
  HttpResponse,
} from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { defer, Observable, filter, map } from 'rxjs';
import { environment } from '../../environment';
import {
  DescribeCompendiumImageRequestDto,
  DescribeImageRequestDto,
} from '../types/dtos/generate/describe-image-request.dto';
import { GenerateTextRequestDto } from '../types/dtos/generate/generate-text-request.dto';
import { GenerateTextResponseChunkDto } from '../types/dtos/generate/generate-text-response-chunk.dto';
import { TextGenerationModelInfoDto } from '../types/dtos/generate/text-generation-model-info.dto';
import { TextGenerationPreviewDto } from '../types/dtos/generate/text-generation-preview.dto';
import { TextGenerationProvider } from '../types/enums/text-generation-provider';
import { NdjsonStreamDecoder } from '../utils/ndjson-stream-decoder';
import {
  GenerateTextCompletion,
  GenerateTextService,
  GenerateTextStreamUpdate,
} from './generate-text.service';

@Injectable()
export class ApiGenerateTextService extends GenerateTextService {
  private http = inject(HttpClient);

  private baseUrl = environment.api.baseUrl;

  generateText(
    request: GenerateTextRequestDto,
  ): Observable<GenerateTextStreamUpdate> {
    this.saveRecentlyUsedModel(request.model);

    return defer(() => {
      const decoder =
        new NdjsonStreamDecoder<GenerateTextResponseChunkDto>();
      let content = '';

      return this.requestTextStream(request).pipe(
        filter(
          (event) =>
            event.type === HttpEventType.DownloadProgress ||
            event.type === HttpEventType.Response,
        ),
        map((event): GenerateTextStreamUpdate | null => {
          const isComplete = event.type === HttpEventType.Response;
          const responseText = isComplete
            ? ((event as HttpResponse<string>).body ?? decoder.rawResponse)
            : ((event as HttpDownloadProgressEvent).partialText ??
              decoder.rawResponse);
          const chunks = decoder.pushCumulative(responseText, isComplete);

          if (chunks.length === 0 && !isComplete) {
            return null;
          }

          content += chunks.map((chunk) => chunk.content).join('');
          return { content, isComplete };
        }),
        filter(
          (update): update is GenerateTextStreamUpdate => update !== null,
        ),
      );
    });
  }

  generateTextCompletion(request: GenerateTextRequestDto): Observable<GenerateTextCompletion> {
    this.saveRecentlyUsedModel(request.model);

    return this.requestTextStream(request)
      .pipe(
        filter((event) => event.type === HttpEventType.Response),
        map((event) => {
          const response = event as HttpResponse<string>;
          const rawResponse = response.body ?? '';
          try {
            const decoder =
              new NdjsonStreamDecoder<GenerateTextResponseChunkDto>();
            const responseChunks = decoder.pushCumulative(rawResponse, true);
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

  private requestTextStream(
    request: GenerateTextRequestDto,
  ): Observable<HttpEvent<string>> {
    return this.http.post(`${this.baseUrl}/generate/text/streamed`, request, {
      observe: 'events',
      reportProgress: true,
      responseType: 'text' as const,
    });
  }

  getGenerationPreview(request: GenerateTextRequestDto): Observable<TextGenerationPreviewDto> {
    this.saveRecentlyUsedModel(request.model);
    return this.http.post<TextGenerationPreviewDto>(
      `${this.baseUrl}/generate/text/preview`,
      request,
    );
  }

  describeImage(
    image: Blob,
    request: DescribeImageRequestDto | DescribeCompendiumImageRequestDto,
  ): Observable<string> {
    this.saveRecentlyUsedModel(request.model);

    const formData = new FormData();
    formData.append('image', image, 'image.png');
    formData.append('model', request.model);
    formData.append('promptId', request.promptId);
    const endpoint = 'compendiumId' in request
      ? 'describe-compendium-image'
      : 'describe-image';

    if ('compendiumId' in request) {
      formData.append('compendiumId', request.compendiumId);
    }

    if (request.instructions !== null && request.instructions.trim() !== '') {
      formData.append('instructions', request.instructions);
    }

    return this.http.post(`${this.baseUrl}/generate/text/${endpoint}`, formData, {
      responseType: 'text' as const,
    });
  }

  protected fetchAvailableModelInfos(
    provider?: TextGenerationProvider | null,
  ): Observable<TextGenerationModelInfoDto[]> {
    const suffix = provider ? `?provider=${provider}` : '';
    return this.http.get<TextGenerationModelInfoDto[]>(
      `${this.baseUrl}/generate/text/models${suffix}`,
    );
  }
}
