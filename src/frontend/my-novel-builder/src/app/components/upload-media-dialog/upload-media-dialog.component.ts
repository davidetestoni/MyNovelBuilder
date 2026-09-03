import { Component, inject } from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';

export interface UploadMediaDialogData {
  initialFile?: File;
  initialName?: string;
}

export interface UploadMediaDialogResult {
  name: string;
  file: File;
}

@Component({
  selector: 'app-upload-media-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, InputTextModule, ButtonModule],
  templateUrl: './upload-media-dialog.component.html',
  styleUrl: './upload-media-dialog.component.scss',
})
export class UploadMediaDialogComponent {
  private dialogRef = inject(DynamicDialogRef);
  private config = inject(DynamicDialogConfig);

  data = (this.config.data ?? {}) as UploadMediaDialogData;
  selectedFile: File | null = this.data.initialFile ?? null;

  formGroup = new FormGroup({
    name: new FormControl(this.data.initialName ?? '', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  onFileSelected(event: Event): void {
    const input = event.target;
    if (!(input instanceof HTMLInputElement)) {
      return;
    }

    const file = input.files?.[0] ?? null;
    this.selectedFile = file;

    if (file && this.formGroup.controls.name.value.trim() === '') {
      this.formGroup.patchValue({
        name: file.name,
      });
    }
  }

  submit(): void {
    if (this.selectedFile === null) {
      return;
    }

    const name = this.formGroup.controls.name.value.trim();
    if (name === '') {
      return;
    }

    this.dialogRef.close({
      name,
      file: this.selectedFile,
    } satisfies UploadMediaDialogResult);
  }
}
