import { HttpEvent } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  DescribeCompendiumImageRequestDto,
  DescribeImageRequestDto,
} from '../types/dtos/generate/describe-image-request.dto';
import { GenerateTextRequestDto } from '../types/dtos/generate/generate-text-request.dto';
import { TextGenerationPreviewDto } from '../types/dtos/generate/text-generation-preview.dto';
import { TextGenerationModelInfoDto } from '../types/dtos/generate/text-generation-model-info.dto';
import { TextGenerationProvider } from '../types/enums/text-generation-provider';

export interface GenerateTextCompletion {
  content: string;
  rawResponse: string;
  parseError: string | null;
}

@Injectable()
export abstract class GenerateTextService {
  abstract generateText(request: GenerateTextRequestDto): Observable<HttpEvent<string>>;
  abstract generateTextCompletion(request: GenerateTextRequestDto): Observable<GenerateTextCompletion>;
  abstract getGenerationPreview(request: GenerateTextRequestDto): Observable<TextGenerationPreviewDto>;
  abstract describeImage(
    image: Blob,
    request: DescribeImageRequestDto | DescribeCompendiumImageRequestDto,
  ): Observable<string>;
  getAvailableModelInfos(
    provider?: TextGenerationProvider | null,
  ): Observable<TextGenerationModelInfoDto[]> {
    return this.fetchAvailableModelInfos(provider);
  }

  protected saveRecentlyUsedModel(model: string): void {
    const recentlyUsedModels = this.getRecentlyUsedModels();
    const index = recentlyUsedModels.indexOf(model);
    if (index !== -1) {
      recentlyUsedModels.splice(index, 1);
    }

    recentlyUsedModels.unshift(model);
    if (recentlyUsedModels.length > 10) {
      recentlyUsedModels.pop();
    }

    localStorage.setItem('recentlyUsedModels', JSON.stringify(recentlyUsedModels));
  }

  getRecentlyUsedModels(): string[] {
    const models = localStorage.getItem('recentlyUsedModels');
    if (!models) {
      return [];
    }

    return JSON.parse(models);
  }

  sortModels(models: string[]): string[] {
    models = [...models].sort();

    const recentlyUsedModels = this.getRecentlyUsedModels().filter((model) =>
      models.includes(model),
    );

    models = models.filter((model) => !recentlyUsedModels.includes(model));
    models.unshift(...recentlyUsedModels);

    return models;
  }

  protected abstract fetchAvailableModelInfos(
    provider?: TextGenerationProvider | null,
  ): Observable<TextGenerationModelInfoDto[]>;
}
