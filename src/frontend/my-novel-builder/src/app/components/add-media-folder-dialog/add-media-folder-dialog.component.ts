import { Component, inject } from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { DynamicDialogRef } from 'primeng/dynamicdialog';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';

export interface AddMediaFolderDialogResult {
  name: string;
  path: string;
}

@Component({
  selector: 'app-add-media-folder-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, InputTextModule, ButtonModule],
  templateUrl: './add-media-folder-dialog.component.html',
  styleUrl: './add-media-folder-dialog.component.scss',
})
export class AddMediaFolderDialogComponent {
  private dialogRef = inject(DynamicDialogRef);

  formGroup = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    path: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  submit(): void {
    const { name, path } = this.formGroup.getRawValue();

    if (!name.trim() || !path.trim()) {
      return;
    }

    this.dialogRef.close({
      name: name.trim(),
      path: path.trim(),
    } satisfies AddMediaFolderDialogResult);
  }
}
