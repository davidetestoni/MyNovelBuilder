import { Component, inject, Inject, OnInit } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { CompendiumService } from '../../services/compendium.service';
import { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import { ToastrService } from 'ngx-toastr';
import { TitleCasePipe } from '@angular/common';
import { NovelService } from '../../services/novel.service';

export interface GenerateCompendiumRecordComponentData {
  generatedText: string;
  novelId: string;
}

@Component({
  selector: 'app-generate-compendium-record-result',
  standalone: true,
  imports: [FormsModule, ReactiveFormsModule, TitleCasePipe],
  templateUrl: './generate-compendium-record-result.component.html',
  styleUrl: './generate-compendium-record-result.component.scss'
})
export class GenerateCompendiumRecordResultComponent implements OnInit {
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

  constructor(
    @Inject(MAT_DIALOG_DATA)
    public data: GenerateCompendiumRecordComponentData,
    public dialogRef: MatDialogRef<GenerateCompendiumRecordResultComponent>
  ) {}

  ngOnInit(): void {
    this.formGroup.get('context')!.setValue(this.data.generatedText);
    
    this.novelService.getNovel(this.data.novelId)
      .subscribe((novel) => {
        this.compendiumService.getCompendia().subscribe((compendia) => {
          this.compendia = compendia
            .filter((compendium) => novel.compendiumIds.includes(compendium.id));

          if (this.compendia.length > 0) {
            this.formGroup.get('compendiumId')!.setValue(this.compendia[0].id);
          }
        });
      }
    );
  }

  accept(): void {
    const name = this.formGroup.get('name')!.value!;

    this.compendiumService.createRecord({
      name: name,
      aliases: this.formGroup.get('aliases')!.value!,
      type: this.formGroup.get('type')!.value!,
      context: this.data.generatedText,
      compendiumId: this.formGroup.get('compendiumId')!.value!,
      alwaysIncluded: false,
    }).subscribe(() => {
      this.toastr.success(`Record ${name} created successfully`);
      this.dialogRef.close(true);
    });
  }
}
