import { Component, inject, OnInit } from '@angular/core';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import {
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { CompendiumService } from '../../services/compendium.service';
import { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import { ToastrService } from 'ngx-toastr';
import { TitleCasePipe } from '@angular/common';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';

// Latest PrimeNG Imports
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { ButtonModule } from 'primeng/button';
import { AliasSuggestionsComponent } from '../alias-suggestions/alias-suggestions.component';
import { CompendiumOptionPreviewComponent } from '../compendium-option-preview/compendium-option-preview.component';
import { finalize, tap } from 'rxjs';

export interface GenerateCompendiumRecordComponentData {
  generatedText: string;
  novelId: string;
}

@Component({
  selector: 'app-generate-compendium-record-result',
  standalone: true,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    TitleCasePipe,
    InputTextModule,
    TextareaModule,
    SelectModule,
    ButtonModule,
    AliasSuggestionsComponent,
    CompendiumOptionPreviewComponent,
  ],
  templateUrl: './generate-compendium-record-result.component.html',
  styleUrl: './generate-compendium-record-result.component.scss',
})
export class GenerateCompendiumRecordResultComponent implements OnInit {
  config = inject(DynamicDialogConfig);
  dialogRef = inject(DynamicDialogRef);

  data!: GenerateCompendiumRecordComponentData;

  readonly compendiumService: CompendiumService = inject(CompendiumService);
  readonly toastr: ToastrService = inject(ToastrService);

  compendia: CompendiumDto[] = [];
  isLoadingCompendia = false;
  isCreating = false;

  recordTypes: CompendiumRecordType[] = [
    CompendiumRecordType.Character,
    CompendiumRecordType.Place,
    CompendiumRecordType.Object,
    CompendiumRecordType.Event,
    CompendiumRecordType.Concept,
    CompendiumRecordType.Other,
  ];

  formGroup = new FormGroup({
    compendiumId: new FormControl('', [Validators.required]),
    type: new FormControl(CompendiumRecordType.Character, [
      Validators.required,
      Validators.pattern(Object.values(CompendiumRecordType).join('|')),
    ]),
    name: new FormControl('', [Validators.required, Validators.maxLength(100)]),
    aliases: new FormControl('', [Validators.maxLength(500)]),
    context: new FormControl(''),
  });

  constructor() {
    this.data = this.config.data as GenerateCompendiumRecordComponentData;
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

  ngOnInit(): void {
    this.formGroup.get('context')!.setValue(this.data.generatedText);

    this.isLoadingCompendia = true;
    this.compendiumService
      .getNovelCompendia(this.data.novelId)
      .pipe(
        tap((compendia) => {
          this.compendia = compendia;

          if (this.compendia.length > 0) {
            this.formGroup
              .get('compendiumId')!
              .setValue(this.compendia[0].id);
          }
        }),
        finalize(() => (this.isLoadingCompendia = false)),
      )
      .subscribe({
        error: () => {
          this.compendia = [];
          this.formGroup.get('compendiumId')!.setValue('');
          this.toastr.error('Failed to load the novel compendia.');
        },
      });
  }

  accept(): void {
    if (
      this.formGroup.invalid ||
      this.isLoadingCompendia ||
      this.isCreating
    ) {
      return;
    }

    const nameControl = this.formGroup.get('name')!;
    const name = nameControl.value?.trim() ?? '';
    if (!name) {
      nameControl.setErrors({ required: true });
      return;
    }

    this.isCreating = true;

    this.compendiumService
      .createRecord({
        name: name,
        aliases: this.formGroup.get('aliases')!.value?.trim() ?? '',
        type: this.formGroup.get('type')!.value!,
        context: this.formGroup.get('context')!.value ?? '',
        compendiumId: this.formGroup.get('compendiumId')!.value!,
        alwaysIncluded: false,
        characterVoiceAssignments: [],
      })
      .pipe(finalize(() => (this.isCreating = false)))
      .subscribe({
        next: () => {
          this.toastr.success(`Record ${name} created successfully`);
          this.dialogRef.close(true);
        },
        error: () => {
          this.toastr.error(`Failed to create record ${name}`);
        },
      });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
