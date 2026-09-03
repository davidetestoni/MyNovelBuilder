import { Component, inject, OnInit } from '@angular/core';
import {
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { WritingTense } from '../../types/enums/writing-tense';
import { WritingPov } from '../../types/enums/writing-pov';
import { WritingLanguage } from '../../types/enums/writing-language';
import { NovelService } from '../../services/novel.service';
import { ToastrModule, ToastrService } from 'ngx-toastr';
import { DynamicDialogRef } from 'primeng/dynamicdialog';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { ButtonModule } from 'primeng/button';
import { FileUploadModule } from 'primeng/fileupload';
import { readImageFileFromClipboard } from '../../utils/clipboard-image';
import { finalize, of, switchMap } from 'rxjs';
import { SelectModule } from 'primeng/select';
import { MultiSelectModule } from 'primeng/multiselect';
import { CompendiumService } from '../../services/compendium.service';
import { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import { CompendiumRecordOverviewDto } from '../../types/dtos/compendium-record/compendium-record-overview.dto';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { SpacedPipe } from '../../pipes/spaced.pipe';
import { CompendiumOptionPreviewComponent } from '../compendium-option-preview/compendium-option-preview.component';
import { OptionPreviewComponent } from '../option-preview/option-preview.component';

@Component({
  selector: 'app-create-novel',
  standalone: true,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    ToastrModule,
    InputTextModule,
    TextareaModule,
    ButtonModule,
    FileUploadModule,
    SelectModule,
    MultiSelectModule,
    SpacedPipe,
    CompendiumOptionPreviewComponent,
    OptionPreviewComponent,
  ],
  templateUrl: './create-novel.component.html',
  styleUrl: './create-novel.component.scss',
})
export class CreateNovelComponent implements OnInit {
  private dialogRef = inject(DynamicDialogRef);
  private toastr = inject(ToastrService);

  imagePreview: string | ArrayBuffer | null = null;
  imageFile: File | null = null;
  isCreating = false;
  compendia: CompendiumDto[] = [];
  readonly novelService: NovelService = inject(NovelService);
  readonly compendiumService: CompendiumService = inject(CompendiumService);

  readonly writingTenses = [WritingTense.Past, WritingTense.Present];
  readonly writingPovs = [
    WritingPov.FirstPerson,
    WritingPov.ThirdPersonLimited,
    WritingPov.ThirdPersonOmniscient,
  ];
  readonly writingLanguages = [
    WritingLanguage.English,
    WritingLanguage.Italian,
    WritingLanguage.French,
    WritingLanguage.Spanish,
    WritingLanguage.German,
    WritingLanguage.Russian,
  ];

  formGroup = new FormGroup({
    title: new FormControl('', [
      Validators.required,
      Validators.maxLength(100),
    ]),
    author: new FormControl('', [Validators.maxLength(100)]),
    brief: new FormControl('', [Validators.maxLength(500)]),
    tense: new FormControl(WritingTense.Present, [
      Validators.required,
      Validators.pattern(Object.values(WritingTense).join('|')),
    ]),
    pov: new FormControl(WritingPov.FirstPerson, [
      Validators.required,
      Validators.pattern(Object.values(WritingPov).join('|')),
    ]),
    language: new FormControl(WritingLanguage.English, [
      Validators.required,
      Validators.pattern(Object.values(WritingLanguage).join('|')),
    ]),
    compendiumIds: new FormControl<string[]>([], { nonNullable: true }),
    mainCharacterId: new FormControl<string | null>(null),
  });

  ngOnInit(): void {
    this.compendiumService.getCompendia().subscribe((compendia) => {
      this.compendia = [...compendia].sort(
        (a, b) =>
          this.getCompendiumTimestamp(b) - this.getCompendiumTimestamp(a),
      );
    });
  }

  getAvailableCharacters(): CompendiumRecordOverviewDto[] {
    const selectedCompendiumIds = this.formGroup.controls.compendiumIds.value;

    return this.compendia
      .filter((compendium) => selectedCompendiumIds.includes(compendium.id))
      .flatMap((compendium) => compendium.records)
      .filter((record) => record.type === CompendiumRecordType.Character)
      .sort((a, b) => a.name.localeCompare(b.name));
  }

  onCompendiaChange(): void {
    const mainCharacterId = this.formGroup.controls.mainCharacterId.value;
    if (
      mainCharacterId !== null &&
      !this.getAvailableCharacters().some(
        (record) => record.id === mainCharacterId,
      )
    ) {
      this.formGroup.controls.mainCharacterId.setValue(null);
    }
  }

  createNovel(): void {
    if (this.formGroup.invalid || this.isCreating) {
      return;
    }

    const tenseValue: string = this.formGroup.get('tense')!.value!;
    const tense: WritingTense = Object.values(WritingTense).find(
      (tense) => tense === tenseValue,
    )!;

    const povValue: string = this.formGroup.get('pov')!.value!;
    const pov: WritingPov = Object.values(WritingPov).find(
      (pov) => pov === povValue,
    )!;

    const languageValue: string = this.formGroup.get('language')!.value!;
    const language: WritingLanguage = Object.values(WritingLanguage).find(
      (language) => language === languageValue,
    )!;

    const imageFile = this.imageFile;
    this.isCreating = true;

    this.novelService
      .createNovel({
        title: this.formGroup.get('title')!.value!,
        author: this.formGroup.get('author')?.value ?? '',
        brief: this.formGroup.get('brief')?.value ?? '',
        tense,
        pov,
        language,
        rpgMode: false,
        mainCharacterId: this.formGroup.controls.mainCharacterId.value,
        compendiumIds: this.formGroup.controls.compendiumIds.value,
      })
      .pipe(
        switchMap((novel) =>
          imageFile === null
            ? of(undefined)
            : this.novelService.uploadNovelCoverImage(novel.id, imageFile),
        ),
        finalize(() => {
          this.isCreating = false;
        }),
      )
      .subscribe({
        next: () => {
          this.toastr.success('Novel created successfully.');
          this.dialogRef.close(true);
        },
        error: () => {
          this.toastr.error('Failed to create novel.');
        },
      });
  }

  onCoverChange(event: Event) {
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

  private getCompendiumTimestamp(compendium: CompendiumDto): number {
    const timestamp = Date.parse(compendium.updatedAt || compendium.createdAt);
    return Number.isNaN(timestamp) ? 0 : timestamp;
  }
}
