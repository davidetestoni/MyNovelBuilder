import { DecimalPipe, TitleCasePipe } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { TextareaModule } from 'primeng/textarea';
import { forkJoin } from 'rxjs';
import { CompendiumService } from '../../services/compendium.service';
import { GenerateTextService } from '../../services/generate-text.service';
import { CompendiumRecordDto } from '../../types/dtos/compendium-record/compendium-record.dto';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { GenerateTextRequestDto } from '../../types/dtos/generate/generate-text-request.dto';
import { TextGenerationModelInfoDto } from '../../types/dtos/generate/text-generation-model-info.dto';
import { TextGenerationPreviewDto } from '../../types/dtos/generate/text-generation-preview.dto';

export interface GenerateTextPreviewComponentData {
  request: GenerateTextRequestDto;
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
  selectedModelInfo: TextGenerationModelInfoDto | null = null;
  includedRecords: CompendiumRecordDto[] = [];
  estimatedInputPrice: number | null = null;
  isLoading = true;
  hasError = false;

  constructor() {
    this.data = this.config.data as GenerateTextPreviewComponentData;
  }

  ngOnInit(): void {
    forkJoin({
      preview: this.generateTextService.getGenerationPreview(this.data.request),
      modelInfos: this.generateTextService.getAvailableModelInfos(),
    }).subscribe({
      next: ({ preview, modelInfos }) => {
        console.log('Infos:', { preview, modelInfos });
        this.preview = preview;
        this.selectedModelInfo =
          modelInfos.find((m) => m.id === this.data.request.model) ?? null;

        if (this.selectedModelInfo !== null) {
          this.estimatedInputPrice =
            preview.inputTokens * this.selectedModelInfo.inputTokenPrice;
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
    const mainImage = record.media.filter((image) => image.isCurrent);
    return mainImage.length > 0 ? mainImage[0].url : null;
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
