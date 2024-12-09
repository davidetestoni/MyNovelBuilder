import { Component, Inject, inject } from '@angular/core';
import {
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import {
  MAT_DIALOG_DATA,
  MatDialogRef,
} from '@angular/material/dialog';
import { ToastrModule, ToastrService } from 'ngx-toastr';
import { CompendiumService } from '../../services/compendium.service';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { TitleCasePipe } from '@angular/common';

@Component({
  selector: 'app-create-compendium-record',
  standalone: true,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    ToastrModule,
    TitleCasePipe,
  ],
  templateUrl: './create-compendium-record.component.html',
  styleUrl: './create-compendium-record.component.scss',
})
export class CreateCompendiumRecordComponent {
  imagePreview: string | ArrayBuffer | null = null;
  imageFile: File | null = null;
  readonly compendiumService: CompendiumService = inject(CompendiumService);

  formGroup = new FormGroup({
    name: new FormControl('', [Validators.required, Validators.maxLength(100)]),
    aliases: new FormControl('', [Validators.maxLength(500)]),
    type: new FormControl(CompendiumRecordType.Character, [
      Validators.required,
      Validators.pattern(Object.values(CompendiumRecordType).join('|')),
    ]),
    context: new FormControl('', [Validators.maxLength(10000)]),
    alwaysIncluded: new FormControl(false),
  });

  recordTypes: CompendiumRecordType[] = [
    CompendiumRecordType.Character,
    CompendiumRecordType.Place,
    CompendiumRecordType.Object,
    CompendiumRecordType.Event,
    CompendiumRecordType.Concept,
    CompendiumRecordType.Other,
  ];

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: { compendiumId: string },
    public dialogRef: MatDialogRef<CreateCompendiumRecordComponent>,
    private toastr: ToastrService
  ) {}

  createRecord(): void {
    this.compendiumService
      .createRecord({
        name: this.formGroup.get('name')!.value!,
        aliases: this.formGroup.get('aliases')?.value ?? '',
        type: this.formGroup.get('type')!.value!,
        context: this.formGroup.get('context')!.value!,
        compendiumId: this.data.compendiumId,
        alwaysIncluded: this.formGroup.get('alwaysIncluded')!.value!,
      })
      .subscribe((record) => {
        if (this.imageFile !== null) {
          this.compendiumService
            .uploadRecordMedia(record.id, this.imageFile, true)
            .subscribe(() => {
              this.toastr.success('Record created successfully');
              this.dialogRef.close(record);
            });
        } else {
          this.toastr.success('Record created successfully');
          this.dialogRef.close(record);
        }
      });
  }

  onImageChange(event: Event) {
    const input = event.target as HTMLInputElement;

    if (input.files && input.files[0]) {
      const file = input.files[0];
      this.imageFile = file;

      const reader = new FileReader();

      reader.onload = (e: ProgressEvent<FileReader>) => {
        const target = e.target as FileReader;
        if (target.result !== undefined) {
          this.imagePreview = target.result;
        }
      };

      reader.readAsDataURL(file);
    }
  }
}
