import { Component, inject } from '@angular/core';
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
import {
  MatDialogActions,
  MatDialogClose,
  MatDialogRef,
} from '@angular/material/dialog';
import { provideAnimations } from '@angular/platform-browser/animations';

@Component({
  selector: 'app-create-novel',
  standalone: true,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    MatDialogActions,
    MatDialogClose,
    ToastrModule,
  ],
  templateUrl: './create-novel.component.html',
  styleUrl: './create-novel.component.scss',
})
export class CreateNovelComponent {
  imagePreview: string | ArrayBuffer | null = null;
  imageFile: File | null = null;
  readonly novelService: NovelService = inject(NovelService);

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
    // TODO: Add compendia and main character id
  });

  constructor(
    public dialogRef: MatDialogRef<CreateNovelComponent>,
    private toastr: ToastrService
  ) {}

  createNovel(): void {
    const tenseValue: string = this.formGroup.get('tense')!.value!;
    const tense: WritingTense = Object.values(WritingTense).find(
      (tense) => tense === tenseValue
    )!;

    const povValue: string = this.formGroup.get('pov')!.value!;
    const pov: WritingPov = Object.values(WritingPov).find(
      (pov) => pov === povValue
    )!;

    const languageValue: string = this.formGroup.get('language')!.value!;
    const language: WritingLanguage = Object.values(WritingLanguage).find(
      (language) => language === languageValue
    )!;

    this.novelService
      .createNovel({
        title: this.formGroup.get('title')!.value!,
        author: this.formGroup.get('author')?.value ?? '',
        brief: this.formGroup.get('brief')?.value ?? '',
        tense,
        pov,
        language,
        mainCharacterId: null,
      })
      .subscribe((novel) => {
        if (this.imageFile !== null) {
          this.novelService
            .uploadNovelCoverImage(novel.id, this.imageFile)
            .subscribe(() => {
              this.toastr.success('Novel created successfully.');
              this.dialogRef.close(true);
            });
        } else {
          this.toastr.success('Novel created successfully.');
          this.dialogRef.close(true);
        }
      });
  }

  onCoverChange(event: Event) {
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
