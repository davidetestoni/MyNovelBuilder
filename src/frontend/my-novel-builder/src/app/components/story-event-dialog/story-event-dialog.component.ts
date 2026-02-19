import { Component, inject } from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { StoryEvent } from '../../types/dtos/novel/prose';

interface StoryEventDialogChapter {
  label: string;
  value: number;
}

export interface StoryEventDialogData {
  mode: 'create' | 'edit';
  chapters: StoryEventDialogChapter[];
  selectedChapterIndex: number;
  storyEvent?: StoryEvent;
}

export interface StoryEventDialogResult {
  chapterIndex: number;
  storyEvent: StoryEvent;
}

@Component({
  selector: 'app-story-event-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    InputTextModule,
    TextareaModule,
    ButtonModule,
    SelectModule,
  ],
  templateUrl: './story-event-dialog.component.html',
  styleUrl: './story-event-dialog.component.scss',
})
export class StoryEventDialogComponent {
  config = inject(DynamicDialogConfig);
  dialogRef = inject(DynamicDialogRef);

  data = (this.config.data || { mode: 'create' }) as StoryEventDialogData;

  formGroup = new FormGroup({
    chapterIndex: new FormControl<number | null>(null, [Validators.required]),
    date: new FormControl('', [Validators.maxLength(200)]),
    title: new FormControl('', [Validators.maxLength(200)]),
    description: new FormControl('', [Validators.maxLength(10000)]),
  });

  ngOnInit(): void {
    this.formGroup.patchValue({
      chapterIndex: this.data.selectedChapterIndex,
    });

    const storyEvent = this.data.storyEvent;
    if (!storyEvent) {
      return;
    }

    this.formGroup.patchValue({
      date: storyEvent.date || '',
      title: storyEvent.title || '',
      description: storyEvent.description || '',
    });
  }

  submit(): void {
    if (this.formGroup.invalid) {
      return;
    }

    const chapterIndex = this.formGroup.get('chapterIndex')!.value;
    if (chapterIndex === null) {
      return;
    }

    const storyEvent: StoryEvent = {
      date: (this.formGroup.get('date')!.value || '').trim(),
      title: (this.formGroup.get('title')!.value || '').trim(),
      description: (this.formGroup.get('description')!.value || '').trim(),
    };

    this.dialogRef.close({
      chapterIndex,
      storyEvent,
    } as StoryEventDialogResult);
  }
}
