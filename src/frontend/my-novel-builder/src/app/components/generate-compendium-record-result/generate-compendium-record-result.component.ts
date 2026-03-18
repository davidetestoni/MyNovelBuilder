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
import { NovelService } from '../../services/novel.service';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';

// Latest PrimeNG Imports
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { ButtonModule } from 'primeng/button';
import { AliasSuggestionsComponent } from '../alias-suggestions/alias-suggestions.component';
import { CompendiumOptionPreviewComponent } from '../compendium-option-preview/compendium-option-preview.component';

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
  readonly novelService: NovelService = inject(NovelService);
  readonly toastr: ToastrService = inject(ToastrService);

  compendia: CompendiumDto[] = [];

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

    this.novelService.getNovel(this.data.novelId).subscribe((novel) => {
      this.compendiumService.getCompendia().subscribe((compendia) => {
        this.compendia = compendia.filter((compendium) =>
          novel.compendiumIds.includes(compendium.id),
        );

        if (this.compendia.length > 0) {
          this.formGroup.get('compendiumId')!.setValue(this.compendia[0].id);
        }
      });
    });
  }

  accept(): void {
    if (this.formGroup.invalid) return;

    const name = this.formGroup.get('name')!.value!;

    this.compendiumService
      .createRecord({
        name: name,
        aliases: this.formGroup.get('aliases')!.value!,
        type: this.formGroup.get('type')!.value!,
        context: this.data.generatedText,
        compendiumId: this.formGroup.get('compendiumId')!.value!,
        alwaysIncluded: false,
      })
      .subscribe(() => {
        this.toastr.success(`Record ${name} created successfully`);
        this.dialogRef.close(true);
      });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
