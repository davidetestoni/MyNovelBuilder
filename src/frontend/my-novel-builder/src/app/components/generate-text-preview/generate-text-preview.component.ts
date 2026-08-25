import { DecimalPipe, TitleCasePipe } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { TextareaModule } from 'primeng/textarea';
import { forkJoin, Observable } from 'rxjs';
import { CompendiumService } from '../../services/compendium.service';
import { GenerateTextService } from '../../services/generate-text.service';
import { CompendiumRecordDto } from '../../types/dtos/compendium-record/compendium-record.dto';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { GenerateTextRequestDto } from '../../types/dtos/generate/generate-text-request.dto';
import { TextGenerationModelInfoDto } from '../../types/dtos/generate/text-generation-model-info.dto';
import { TextGenerationPreviewDto } from '../../types/dtos/generate/text-generation-preview.dto';

export interface GenerateTextPreviewComponentData {
  request?: GenerateTextRequestDto;
  model?: string;
  items?: GenerateTextPreviewItemSource[];
}

export interface GenerateTextPreviewItemSource {
  label?: string;
  request?: GenerateTextRequestDto;
  preview?: Observable<TextGenerationPreviewDto>;
}

export interface GenerateTextPreviewItem {
  label?: string;
  preview: TextGenerationPreviewDto;
}

@Component({
  selector: 'app-generate-text-preview',
  standalone: true,
  imports: [TextareaModule, TitleCasePipe, DecimalPipe],
  templateUrl: './generate-text-preview.component.html',
  styleUrl: './generate-text-preview.component.scss',
})
export class GenerateTextPreviewComponent implements OnInit {
  config = inject(DynamicDialogConfig);
  dialogRef = inject(DynamicDialogRef);
  readonly generateTextService: GenerateTextService =
    inject(GenerateTextService);
  readonly compendiumService: CompendiumService = inject(CompendiumService);

  data!: GenerateTextPreviewComponentData;
  preview: TextGenerationPreviewDto | null = null;
  previewItems: GenerateTextPreviewItem[] = [];
  model = '';
  selectedModelInfo: TextGenerationModelInfoDto | null = null;
  includedRecords: CompendiumRecordDto[] = [];
  estimatedInputPrice: number | null = null;
  isLoading = true;
  hasError = false;

  constructor() {
    this.data = this.config.data as GenerateTextPreviewComponentData;
  }

  ngOnInit(): void {
    const sources = this.getSources();
    this.model = this.data.request?.model ?? this.data.model ?? '';
    if (sources.length === 0 || !this.model) {
      this.hasError = true;
      this.isLoading = false;
      return;
    }

    forkJoin({
      previews: forkJoin(sources.map((source) => this.loadPreview(source))),
      modelInfos: this.generateTextService.getAvailableModelInfos(),
    }).subscribe({
      next: ({ previews, modelInfos }) => {
        this.previewItems = previews.map((preview, index) => ({
          label: sources[index].label,
          preview,
        }));
        this.preview =
          previews.length === 1 ? previews[0] : this.combinePreviews(previews);
        this.selectedModelInfo =
          modelInfos.find((modelInfo) => modelInfo.id === this.model) ?? null;

        if (this.selectedModelInfo !== null) {
          this.estimatedInputPrice =
            this.preview.inputTokens * this.selectedModelInfo.inputTokenPrice;
        }

        this.loadIncludedRecords();
      },
      error: () => {
        this.hasError = true;
        this.isLoading = false;
      },
    });
  }

  getRecordImage(record: CompendiumRecordDto): string | null {
    return record.media.find((image) => image.isCurrent)?.url ?? null;
  }

  private getSources(): GenerateTextPreviewItemSource[] {
    if (this.data.request) {
      return [{ request: this.data.request }];
    }

    return this.data.items ?? [];
  }

  private loadPreview(
    source: GenerateTextPreviewItemSource,
  ): Observable<TextGenerationPreviewDto> {
    if (source.request) {
      return this.generateTextService.getGenerationPreview(source.request);
    }

    if (source.preview) {
      return source.preview;
    }

    throw new Error('A prompt preview source is required.');
  }

  private combinePreviews(
    previews: TextGenerationPreviewDto[],
  ): TextGenerationPreviewDto {
    return {
      inputTokens: previews.reduce(
        (total, preview) => total + preview.inputTokens,
        0,
      ),
      includedCompendiumRecordIds: [
        ...new Set(
          previews.flatMap((preview) => preview.includedCompendiumRecordIds),
        ),
      ],
      finalMessages: previews.flatMap((preview) => preview.finalMessages),
    };
  }

  private loadIncludedRecords(): void {
    if (
      this.preview === null ||
      this.preview.includedCompendiumRecordIds.length === 0
    ) {
      this.isLoading = false;
      return;
    }

    this.compendiumService
      .getRecordsByIds(this.preview.includedCompendiumRecordIds)
      .subscribe({
        next: (records) => {
          const typeOrder: CompendiumRecordType[] = [
            CompendiumRecordType.Character,
            CompendiumRecordType.Place,
            CompendiumRecordType.Object,
            CompendiumRecordType.Event,
            CompendiumRecordType.Concept,
            CompendiumRecordType.Other,
          ];

          this.includedRecords = [...records].sort((a, b) => {
            const typeDiff =
              typeOrder.indexOf(a.type) - typeOrder.indexOf(b.type);
            if (typeDiff !== 0) {
              return typeDiff;
            }

            return a.name.localeCompare(b.name, undefined, {
              sensitivity: 'base',
            });
          });
        },
        error: () => {
          this.hasError = true;
          this.isLoading = false;
        },
        complete: () => {
          this.isLoading = false;
        },
      });
  }
}
