import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  DescribeCompendiumImageRequestDto,
  DescribeImageRequestDto,
} from '../types/dtos/generate/describe-image-request.dto';
import { GenerateTextRequestDto } from '../types/dtos/generate/generate-text-request.dto';
import { TextGenerationPreviewDto } from '../types/dtos/generate/text-generation-preview.dto';
import { TextGenerationModelInfoDto } from '../types/dtos/generate/text-generation-model-info.dto';
import { TextGenerationProvider } from '../types/enums/text-generation-provider';
import { LocalStorageService } from './local-storage.service';

export interface GenerateTextCompletion {
  content: string;
  rawResponse: string;
  parseError: string | null;
}

export interface GenerateTextStreamUpdate {
  content: string;
  isComplete: boolean;
}

@Injectable()
export abstract class GenerateTextService {
  private readonly localStorageService = inject(LocalStorageService);
  private readonly recentlyUsedModelsKey = 'recentlyUsedModels';

  abstract generateText(
    request: GenerateTextRequestDto,
  ): Observable<GenerateTextStreamUpdate>;
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

    this.localStorageService.setObjectForKey(
      this.recentlyUsedModelsKey,
      recentlyUsedModels,
    );
  }

  getRecentlyUsedModels(): string[] {
    const models = this.localStorageService.getObjectForKey<unknown>(
      this.recentlyUsedModelsKey,
    );
    if (!Array.isArray(models)) {
      return [];
    }

    return models.filter((model): model is string => typeof model === 'string');
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
