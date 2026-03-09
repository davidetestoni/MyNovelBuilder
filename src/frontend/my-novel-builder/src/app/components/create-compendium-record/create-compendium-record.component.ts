import { Component, inject } from '@angular/core';
import {
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import {
  DialogService,
  DynamicDialogConfig,
  DynamicDialogRef,
} from 'primeng/dynamicdialog';
import { ToastrModule, ToastrService } from 'ngx-toastr';
import { CompendiumService } from '../../services/compendium.service';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { TitleCasePipe } from '@angular/common';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { CheckboxModule } from 'primeng/checkbox';
import { ButtonModule } from 'primeng/button';
import { AliasSuggestionsComponent } from '../alias-suggestions/alias-suggestions.component';
import { DescribeImageComponent } from '../describe-image/describe-image.component';
import { TooltipModule } from 'primeng/tooltip';
import { readImageFileFromClipboard } from '../../utils/clipboard-image';

@Component({
  selector: 'app-create-compendium-record',
  standalone: true,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    ToastrModule,
    TitleCasePipe,
    InputTextModule,
    TextareaModule,
    SelectModule,
    CheckboxModule,
    ButtonModule,
    AliasSuggestionsComponent,
    TooltipModule,
  ],
  providers: [DialogService],
  templateUrl: './create-compendium-record.component.html',
  styleUrl: './create-compendium-record.component.scss',
})
export class CreateCompendiumRecordComponent {
  config = inject(DynamicDialogConfig);
  dialogRef = inject(DynamicDialogRef);
  private dialogService = inject(DialogService);
  private toastr = inject(ToastrService);
  private describeImageDialogRef: DynamicDialogRef | null = null;

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

  ngOnDestroy(): void {
    if (this.describeImageDialogRef) {
      this.describeImageDialogRef.close();
    }
  }

  addAlias(alias: string): void {
    const currentAliasesValue = this.formGroup.get('aliases')?.value || '';
    const currentAliases = currentAliasesValue
      .split(',')
      .map((a) => a.trim())
      .filter((a) => a.length > 0);

    if (!currentAliases.some((a) => a.toLowerCase() === alias.toLowerCase())) {
      currentAliases.push(alias);
      this.formGroup.get('aliases')?.setValue(currentAliases.join(', '));
      this.formGroup.get('aliases')?.markAsDirty();
    }
  }

  createRecord(): void {
    this.compendiumService
      .createRecord({
        name: this.formGroup.get('name')!.value!,
        aliases: this.formGroup.get('aliases')?.value ?? '',
        type: this.formGroup.get('type')!.value!,
        context: this.formGroup.get('context')!.value!,
        compendiumId: this.config.data.compendiumId,
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
      this.setImageFile(input.files[0]);
    }
  }

  async readImageFromClipboard(): Promise<void> {
    try {
      const file = await readImageFileFromClipboard();
      this.setImageFile(file);
    } catch (error) {
      this.toastr.error(
        error instanceof Error
          ? error.message
          : 'Failed to read image from clipboard.',
      );
    }
  }

  generateContextFromImage(): void {
    if (this.imageFile === null) {
      return;
    }

    this.describeImageDialogRef = this.dialogService.open(DescribeImageComponent, {
      header: 'Describe Image',
      width: '70vw',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      closable: true,
      closeOnEscape: true,
      modal: true,
      dismissableMask: true,
      data: {
        image: this.imageFile,
        compendiumId: this.config.data.compendiumId,
      },
    });

    this.describeImageDialogRef?.onClose.subscribe((description: string) => {
      if (!description || description.trim() === '') {
        return;
      }

      const currentContext = this.formGroup.get('context')!.value?.trim() ?? '';
      const updatedContext =
        currentContext.length > 0
          ? `${currentContext}\n\n${description.trim()}`
          : description.trim();

      this.formGroup.get('context')!.setValue(updatedContext);
      this.formGroup.get('context')!.markAsDirty();
      this.formGroup.get('context')!.markAsTouched();
    });
  }

  private setImageFile(file: File): void {
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
