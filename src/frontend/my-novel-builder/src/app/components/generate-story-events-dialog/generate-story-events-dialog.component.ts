import { Component, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { SelectModule } from 'primeng/select';
import { ButtonModule } from 'primeng/button';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { PromptType } from '../../types/enums/prompt-type';
import {
  GenerateTextCompletion,
  GenerateTextService,
} from '../../services/generate-text.service';
import { LocalStorageService } from '../../services/local-storage.service';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { StoryEvent } from '../../types/dtos/novel/prose';
import {
  CreateStoryEventsContextInfoDto,
  GenerateTextRequestDto,
  NovelTextGenerationType,
} from '../../types/dtos/generate/generate-text-request.dto';
import { firstValueFrom } from 'rxjs';
import { PromptSelectComponent } from '../prompt-select/prompt-select.component';
import { ModelSelectComponent } from '../model-select/model-select.component';

interface GenerateStoryEventsDialogChapter {
  label: string;
  value: number;
}

export interface GenerateStoryEventsDialogData {
  chapters: GenerateStoryEventsDialogChapter[];
  selectedChapterIndex: number | null;
  prompts: PromptDto[];
  novelId: string;
}

export interface GenerateStoryEventsDialogResult {
  chapters: { chapterIndex: number; storyEvents: StoryEvent[] }[];
}

interface GeneratedStoryEventsPreview {
  chapterIndex: number;
  chapterTitle: string;
  storyEvents: StoryEvent[];
  error: string | null;
  rawOutput: string | null;
}

@Component({
  selector: 'app-generate-story-events-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    SelectModule,
    ButtonModule,
    PromptSelectComponent,
    ModelSelectComponent,
  ],
  templateUrl: './generate-story-events-dialog.component.html',
  styleUrl: './generate-story-events-dialog.component.scss',
})
export class GenerateStoryEventsDialogComponent implements OnDestroy {
  config = inject(DynamicDialogConfig);
  dialogRef = inject(DynamicDialogRef);

  readonly generateTextService: GenerateTextService =
    inject(GenerateTextService);
  readonly localStorageService: LocalStorageService =
    inject(LocalStorageService);
  PromptType = PromptType;

  data: GenerateStoryEventsDialogData;
  promptCount = 0;
  generatedPreviews: GeneratedStoryEventsPreview[] = [];
  isGenerating = false;
  generationError: string | null = null;
  private generationRequestId = 0;

  formGroup = new FormGroup({
    chapterIndex: new FormControl<number | null>(null, [Validators.required]),
    promptId: new FormControl('', [Validators.required]),
    model: new FormControl('', [Validators.required]),
  });

  constructor() {
    this.data = this.config.data as GenerateStoryEventsDialogData;

    this.formGroup.patchValue({
      chapterIndex: this.data.selectedChapterIndex,
    });
  }

  async generate(): Promise<void> {
    if (this.formGroup.invalid || this.isGenerating) {
      return;
    }

    const promptId = (this.formGroup.get('promptId')!.value ?? '').trim();
    const model = (this.formGroup.get('model')!.value ?? '').trim();
    if (!promptId || !model) {
      return;
    }

    this.localStorageService.setNestedStringForKey(
      LocalStorageKey.RecentPrompts,
      PromptType.CreateStoryEvents,
      promptId,
    );

    this.isGenerating = true;
    this.generationError = null;
    this.generatedPreviews = [];
    const requestId = ++this.generationRequestId;

    const selectedChapterIndex = this.formGroup.get('chapterIndex')!.value;
    if (selectedChapterIndex === null) {
      this.isGenerating = false;
      this.generationError = 'Please select a chapter.';
      return;
    }

    const chapterIndex = selectedChapterIndex;
    const chapterTitle =
      this.data.chapters.find((chapter) => chapter.value === chapterIndex)
        ?.label ?? `Chapter ${chapterIndex + 1}`;
    const request: GenerateTextRequestDto = {
      model,
      promptId,
      contextInfo: <CreateStoryEventsContextInfoDto>{
        $type: NovelTextGenerationType.CreateStoryEvents,
        novelId: this.data.novelId,
        chapterIndex,
      },
    };

    try {
      const completion: GenerateTextCompletion = await firstValueFrom(
        this.generateTextService.generateTextCompletion(request),
      );
      if (requestId !== this.generationRequestId) {
        return;
      }

      if (completion.parseError) {
        this.generatedPreviews = [
          {
            chapterIndex,
            chapterTitle,
            storyEvents: [],
            error: `Unable to read the streamed response: ${completion.parseError}`,
            rawOutput: completion.rawResponse || null,
          },
        ];
        return;
      }

      const rawOutput = completion.content.trim();
      const parsedStoryEvents = this.parseStoryEvents(rawOutput);

      this.generatedPreviews = [
        parsedStoryEvents === null
          ? {
              chapterIndex,
              chapterTitle,
              storyEvents: [],
              error:
                'The generated output is not valid JSON or does not match the expected format.',
              rawOutput: rawOutput || completion.rawResponse || null,
            }
          : {
              chapterIndex,
              chapterTitle,
              storyEvents: parsedStoryEvents,
              error: null,
              rawOutput: null,
            },
      ];
    } catch (error) {
      if (requestId !== this.generationRequestId) {
        return;
      }

      this.generatedPreviews = [
        {
          chapterIndex,
          chapterTitle,
          storyEvents: [],
          error: 'Failed to generate story events.',
          rawOutput: error instanceof Error ? error.message : null,
        },
      ];
    } finally {
      if (requestId === this.generationRequestId) {
        this.isGenerating = false;
      }
    }
  }

  accept(): void {
    if (!this.canAccept) {
      return;
    }

    const chapters = this.generatedPreviews
      .filter((preview) => preview.storyEvents.length > 0)
      .map((preview) => ({
        chapterIndex: preview.chapterIndex,
        storyEvents: preview.storyEvents,
      }));

    this.dialogRef.close({
      chapters,
    } as GenerateStoryEventsDialogResult);
  }

  get canAccept(): boolean {
    return (
      this.generatedPreviews.some((preview) => preview.storyEvents.length > 0) &&
      !this.isGenerating
    );
  }

  private parseStoryEvents(rawOutput: string): StoryEvent[] | null {
    if (!rawOutput) {
      return null;
    }

    try {
      const parsed = JSON.parse(rawOutput) as unknown;
      if (!Array.isArray(parsed)) {
        return null;
      }

      const storyEvents: StoryEvent[] = [];
      for (const item of parsed) {
        if (
          !item ||
          typeof item !== 'object' ||
          typeof item.title !== 'string' ||
          typeof item.date !== 'string' ||
          typeof item.description !== 'string'
        ) {
          return null;
        }

        storyEvents.push({
          title: item.title.trim(),
          date: item.date.trim(),
          description: item.description.trim(),
        });
      }

      return storyEvents;
    } catch {
      return null;
    }
  }

  onPromptOptionsChanged(count: number): void {
    this.promptCount = count;
  }

  ngOnDestroy(): void {
    this.generationRequestId++;
    this.isGenerating = false;
  }
}
