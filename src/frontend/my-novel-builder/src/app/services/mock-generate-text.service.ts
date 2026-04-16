import { HttpEvent } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  DescribeCompendiumImageRequestDto,
  DescribeImageRequestDto,
} from '../types/dtos/generate/describe-image-request.dto';
import { GenerateTextRequestDto } from '../types/dtos/generate/generate-text-request.dto';
import { TextGenerationModelInfoDto } from '../types/dtos/generate/text-generation-model-info.dto';
import { TextGenerationPreviewDto } from '../types/dtos/generate/text-generation-preview.dto';
import { TextGenerationProvider } from '../types/enums/text-generation-provider';
import {
  mockedTextCompletionContent,
  mockedTextGenerationModelInfos,
  mockedTextGenerationResponse,
} from './mocks/mock-generate-text.data';
import { mockObservable } from './mocks/mock-utils';
import { GenerateTextCompletion, GenerateTextService } from './generate-text.service';

@Injectable()
export class MockGenerateTextService extends GenerateTextService {
  generateText(request: GenerateTextRequestDto): Observable<HttpEvent<string>> {
    this.saveRecentlyUsedModel(request.model);
    return mockedTextGenerationResponse(
      'The lantern cast a *soft amber glow* across the room, and the letter contained **one unmistakable warning**.',
    );
  }

  generateTextCompletion(request: GenerateTextRequestDto): Observable<GenerateTextCompletion> {
    this.saveRecentlyUsedModel(request.model);

    const mockedContent = mockedTextCompletionContent();
    return mockObservable({
      content: mockedContent,
      rawResponse: mockedContent,
      parseError: null,
    });
  }

  getGenerationPreview(request: GenerateTextRequestDto): Observable<TextGenerationPreviewDto> {
    this.saveRecentlyUsedModel(request.model);
    return mockObservable({
      inputTokens: 0,
      includedCompendiumRecordIds: [],
      finalMessages: [],
    });
  }

  describeImage(
    _image: Blob,
    request: DescribeImageRequestDto | DescribeCompendiumImageRequestDto,
  ): Observable<string> {
    this.saveRecentlyUsedModel(request.model);
    return mockObservable('A generated image description. This is mocked data from the frontend service.');
  }

  protected fetchAvailableModelInfos(
    _provider?: TextGenerationProvider | null,
  ): Observable<TextGenerationModelInfoDto[]> {
    return mockObservable(mockedTextGenerationModelInfos);
  }
}
