import { Component, inject } from '@angular/core';
import { DynamicDialogRef } from 'primeng/dynamicdialog';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-image-source-selector',
  standalone: true,
  imports: [ButtonModule],
  templateUrl: './image-source-selector.component.html',
  styleUrl: './image-source-selector.component.scss',
})
export class ImageSourceSelectorComponent {
  dialogRef = inject(DynamicDialogRef);

  select(source: 'upload' | 'generate') {
    this.dialogRef.close(source);
  }
}
