import { Component, inject } from '@angular/core';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { ButtonModule } from 'primeng/button';

export interface ImageSourceSelectorComponentData {
  uploadLabel?: string;
  generateLabel?: string;
}

@Component({
  selector: 'app-image-source-selector',
  standalone: true,
  imports: [ButtonModule],
  templateUrl: './image-source-selector.component.html',
  styleUrl: './image-source-selector.component.scss',
})
export class ImageSourceSelectorComponent {
  dialogRef = inject(DynamicDialogRef);
  config = inject(DynamicDialogConfig);

  get uploadLabel(): string {
    return (
      (this.config.data as ImageSourceSelectorComponentData | undefined)
        ?.uploadLabel ?? 'Upload Image'
    );
  }

  get generateLabel(): string {
    return (
      (this.config.data as ImageSourceSelectorComponentData | undefined)
        ?.generateLabel ?? 'Generate Image'
    );
  }

  select(source: 'upload' | 'generate') {
    this.dialogRef.close(source);
  }
}
