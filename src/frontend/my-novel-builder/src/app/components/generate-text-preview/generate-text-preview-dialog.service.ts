import { Injectable, inject } from '@angular/core';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { Observable } from 'rxjs';
import { GenerateTextRequestDto } from '../../types/dtos/generate/generate-text-request.dto';
import { TextGenerationPreviewDto } from '../../types/dtos/generate/text-generation-preview.dto';
import {
  GenerateTextPreviewComponent,
  GenerateTextPreviewComponentData,
  GenerateTextPreviewItemSource,
} from './generate-text-preview.component';

export interface GenerateTextPreviewRequestItem {
  label?: string;
  request: GenerateTextRequestDto;
}

@Injectable()
export class GenerateTextPreviewDialogService {
  private readonly dialogService = inject(DialogService);

  open(request: GenerateTextRequestDto): DynamicDialogRef | null {
    return this.openDialog({ request });
  }

  openBatch(items: GenerateTextPreviewRequestItem[]): DynamicDialogRef | null {
    if (items.length === 0) {
      return null;
    }

    return this.openDialog({
      model: items[0].request.model,
      items,
    });
  }

  openPreview(
    model: string,
    preview: Observable<TextGenerationPreviewDto>,
    label?: string,
  ): DynamicDialogRef | null {
    const item: GenerateTextPreviewItemSource = { label, preview };
    return this.openDialog({ model, items: [item] });
  }

  private openDialog(
    data: GenerateTextPreviewComponentData,
  ): DynamicDialogRef | null {
    return this.dialogService.open(GenerateTextPreviewComponent, {
      header: 'Prompt Preview',
      width: '50vw',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      modal: true,
      closable: true,
      closeOnEscape: true,
      dismissableMask: true,
      focusOnShow: false,
      data,
    });
  }
}
