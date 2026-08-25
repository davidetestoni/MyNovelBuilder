import { Injectable, inject } from '@angular/core';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { GenerateTextRequestDto } from '../../types/dtos/generate/generate-text-request.dto';
import { GenerateTextPreviewComponent } from './generate-text-preview.component';

@Injectable()
export class GenerateTextPreviewDialogService {
  private readonly dialogService = inject(DialogService);

  open(request: GenerateTextRequestDto): DynamicDialogRef | null {
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
      data: { request },
    });
  }
}
