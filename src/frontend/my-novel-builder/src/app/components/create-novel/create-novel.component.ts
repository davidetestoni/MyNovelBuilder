import { Component, EventEmitter, Inject, Output } from '@angular/core';
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
import { NovelDto } from '../../types/dtos/novel/novel.dto';
import { MatButtonModule } from '@angular/material/button';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import {
  MatDialogActions,
  MatDialogClose,
  MatDialogContent,
  MatDialogTitle,
} from '@angular/material/dialog';

@Component({
  selector: 'app-create-novel',
  standalone: true,
  imports: [
    MatFormFieldModule,
    MatInputModule,
    FormsModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogTitle,
    MatDialogContent,
    MatDialogActions,
    MatDialogClose,
  ],
  templateUrl: './create-novel.component.html',
  styleUrl: './create-novel.component.scss',
})
export class CreateNovelComponent {
  @Output() created = new EventEmitter<NovelDto>();

  formGroup = new FormGroup({
    title: new FormControl(''),
    author: new FormControl(''),
    brief: new FormControl(''),
    tense: new FormControl('', [
      Validators.required,
      Validators.pattern(Object.values(WritingTense).join('|')),
    ]),
    pov: new FormControl('', [
      Validators.required,
      Validators.pattern(Object.values(WritingPov).join('|')),
    ]),
    language: new FormControl('', [
      Validators.required,
      Validators.pattern(Object.values(WritingLanguage).join('|')),
    ]),
    // TODO: Add compendia and main character id
  });

  constructor(@Inject(NovelService) private novelService: NovelService) {}

  createNovel(): void {
    const tenseValue: string = this.formGroup.get('tense')?.value ?? '';
    const tense: WritingTense =
      WritingTense[tenseValue as keyof typeof WritingTense];

    const povValue: string = this.formGroup.get('pov')?.value ?? '';
    const pov: WritingPov = WritingPov[povValue as keyof typeof WritingPov];

    const languageValue: string = this.formGroup.get('language')?.value ?? '';
    const language: WritingLanguage =
      WritingLanguage[languageValue as keyof typeof WritingLanguage];

    this.novelService
      .createNovel({
        title: this.formGroup.get('title')?.value ?? '',
        author: this.formGroup.get('author')?.value ?? '',
        brief: this.formGroup.get('brief')?.value ?? '',
        tense,
        pov,
        language,
        mainCharacterId: null,
      })
      .subscribe((novel) => {
        this.created.emit(novel);
        this.formGroup.reset();
      });
  }

  onCoverChange($event: Event) {
    throw new Error('Method not implemented.');
  }
}
