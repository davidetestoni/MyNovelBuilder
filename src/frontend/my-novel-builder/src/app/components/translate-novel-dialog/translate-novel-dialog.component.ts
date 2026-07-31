import { CommonModule } from '@angular/common';
import { Component, OnDestroy, ViewChild, inject } from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ToastrModule, ToastrService } from 'ngx-toastr';
import { ButtonModule } from 'primeng/button';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { PromptSelectComponent } from '../prompt-select/prompt-select.component';
import { ModelSelectComponent } from '../model-select/model-select.component';
import {
  GenerateTextCompletion,
  GenerateTextService,
} from '../../services/generate-text.service';
import { NovelService } from '../../services/novel.service';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { PromptType } from '../../types/enums/prompt-type';
import { WritingLanguage } from '../../types/enums/writing-language';
import {
  GenerateTextRequestDto,
  NovelTextGenerationType,
  TranslateNovelContextInfoDto,
} from '../../types/dtos/generate/generate-text-request.dto';
import { Prose, Section, StoryEvent } from '../../types/dtos/novel/prose';
import { NovelDto } from '../../types/dtos/novel/novel.dto';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { LocalStorageService } from '../../services/local-storage.service';
import { SpacedPipe } from '../../pipes/spaced.pipe';

interface TranslationSectionResult {
  sectionIndex: number;
  summary: string;
  text: string;
}

interface TranslationChapterResult {
  chapterTitle: string;
  storyEvents: StoryEvent[];
  sections: TranslationSectionResult[];
}

interface TranslationProgressItem {
  chapterIndex: number;
  chapterTitle: string;
  isCompleted: boolean;
}

export interface TranslateNovelDialogData {
  novel: NovelDto;
  prose: Prose;
  prompts: PromptDto[];
}

export interface TranslateNovelDialogResult {
  novelId: string;
}

@Component({
  selector: 'app-translate-novel-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    ToastrModule,
    SelectModule,
    ButtonModule,
    InputTextModule,
    PromptSelectComponent,
    ModelSelectComponent,
    SpacedPipe,
  ],
  templateUrl: './translate-novel-dialog.component.html',
  styleUrl: './translate-novel-dialog.component.scss',
})
export class TranslateNovelDialogComponent implements OnDestroy {
  @ViewChild(PromptSelectComponent) promptSelect?: PromptSelectComponent;
  @ViewChild(ModelSelectComponent) modelSelect?: ModelSelectComponent;

  config = inject(DynamicDialogConfig);
  dialogRef = inject(DynamicDialogRef);
  private toastrService = inject(ToastrService);

  readonly generateTextService = inject(GenerateTextService);
  readonly novelService = inject(NovelService);
  readonly localStorageService = inject(LocalStorageService);

  readonly data = this.config.data as TranslateNovelDialogData;
  readonly promptType = PromptType.TranslateNovel;
  readonly writingLanguages = Object.values(WritingLanguage);

  promptCount = 0;
  isGenerating = false;
  isSaving = false;
  generationError: string | null = null;
  translatedProse: Prose | null = null;
  progressItems: TranslationProgressItem[] = [];
  private generationRequestId = 0;
  private translatedLanguage: WritingLanguage | null = null;

  formGroup = new FormGroup({
    title: new FormControl('', [Validators.maxLength(100)]),
    targetLanguage: new FormControl<WritingLanguage | null>(null, [Validators.required]),
    promptId: new FormControl('', [Validators.required]),
    model: new FormControl('', [Validators.required]),
    instructions: new FormControl('', [Validators.maxLength(5_000)]),
  });

  constructor() {
    this.formGroup.patchValue({
      title: '',
      targetLanguage: this.getDefaultTargetLanguage(),
    });
  }

  async generate(): Promise<void> {
    const promptId = this.selectedPromptId;
    const model = this.selectedModelId;
    if (!this.canGenerate || !promptId || !model) {
      return;
    }

    const targetLanguage = this.formGroup.get('targetLanguage')!.value;
    if (!targetLanguage || targetLanguage === this.data.novel.language) {
      this.generationError = 'Please choose a target language different from the source novel language.';
      return;
    }

    const instructions = this.normalizeOptionalText(this.formGroup.get('instructions')!.value);

    this.localStorageService.setNestedStringForKey(
      LocalStorageKey.RecentPrompts,
      PromptType.TranslateNovel,
      promptId,
    );

    this.isGenerating = true;
    this.generationError = null;
    this.translatedProse = null;
    this.translatedLanguage = null;
    this.progressItems = this.data.prose.chapters.map(
      (chapter, chapterIndex) => ({
        chapterIndex,
        chapterTitle: chapter.title,
        isCompleted: false,
      }),
    );
    const requestId = ++this.generationRequestId;

    try {
      const translatedChapters: Prose['chapters'] = [];

      for (
        let chapterIndex = 0;
        chapterIndex < this.data.prose.chapters.length;
        chapterIndex++
      ) {
        const chapter = this.data.prose.chapters[chapterIndex];
        const translatedSections: Section[] = Array.from(
          { length: chapter.sections.length },
          () => ({
            summary: '',
            text: '',
            images: [],
            recordOverrides: [],
          }),
        );
        let translatedChapterTitle = chapter.title;
        let translatedStoryEvents: StoryEvent[] = [...chapter.storyEvents];
        const request: GenerateTextRequestDto = {
          model,
          promptId,
          contextInfo: <TranslateNovelContextInfoDto>{
            $type: NovelTextGenerationType.TranslateNovel,
            novelId: this.data.novel.id,
            chapterIndex,
            targetLanguage,
            instructions,
          },
        };

        const completion = await firstValueFrom(
          this.generateTextService.generateTextCompletion(request),
        );
        if (requestId !== this.generationRequestId) {
          return;
        }

        const parsed = this.parseTranslationResult(completion);
        if (!parsed) {
          throw new Error(
            `Unable to parse translated output for chapter ${chapterIndex + 1}.`,
          );
        }

        const expectedSectionIndexes = new Set<number>();
        for (let i = 0; i < chapter.sections.length; i++) {
          expectedSectionIndexes.add(i);
        }

        translatedChapterTitle =
          parsed.chapterTitle.trim() || translatedChapterTitle;
        translatedStoryEvents = parsed.storyEvents;

        for (const translatedSection of parsed.sections) {
          const sourceSection = chapter.sections[translatedSection.sectionIndex];
          if (!sourceSection) {
            throw new Error(
              `The generated output referenced invalid section index ${translatedSection.sectionIndex} for chapter ${chapterIndex + 1}.`,
            );
          }

          translatedSections[translatedSection.sectionIndex] = {
            summary: translatedSection.summary,
            text: translatedSection.text,
            images: [...sourceSection.images],
            recordOverrides: [...sourceSection.recordOverrides],
          };

          expectedSectionIndexes.delete(translatedSection.sectionIndex);
        }

        if (expectedSectionIndexes.size > 0) {
          throw new Error(
            `The generated output did not include every section in chapter ${chapterIndex + 1}.`,
          );
        }

        this.progressItems[chapterIndex] = {
          ...this.progressItems[chapterIndex],
          chapterTitle: translatedChapterTitle,
          isCompleted: true,
        };

        translatedChapters.push({
          title: translatedChapterTitle,
          storyEvents: translatedStoryEvents,
          sections: translatedSections,
        });
      }

      this.translatedProse = {
        chapters: translatedChapters,
      };
      this.translatedLanguage = targetLanguage;
    } catch (error) {
      if (requestId !== this.generationRequestId) {
        return;
      }

      this.generationError =
        error instanceof Error
          ? error.message
          : 'Failed to translate the novel.';
      this.translatedProse = null;
      this.translatedLanguage = null;
    } finally {
      if (requestId === this.generationRequestId) {
        this.isGenerating = false;
      }
    }
  }

  async accept(): Promise<void> {
    if (!this.canAccept) {
      return;
    }

    const translatedProse = this.translatedProse!;

    const targetLanguage = this.formGroup.get('targetLanguage')!.value!;
    const title =
      this.normalizeOptionalText(this.formGroup.get('title')!.value) ??
      `${this.data.novel.title} (${targetLanguage})`;

    this.isSaving = true;

    try {
      const createdNovel = await firstValueFrom(
        this.novelService.createNovel({
          title,
          author: this.data.novel.author,
          brief: this.data.novel.brief,
          tense: this.data.novel.tense,
          pov: this.data.novel.pov,
          language: targetLanguage,
          rpgMode: this.data.novel.rpgMode,
          mainCharacterId: this.data.novel.mainCharacterId,
        }),
      );

      await firstValueFrom(
        this.novelService.updateNovel({
          id: createdNovel.id,
          title: createdNovel.title,
          author: createdNovel.author,
          brief: createdNovel.brief,
          tense: createdNovel.tense,
          pov: createdNovel.pov,
          language: createdNovel.language,
          rpgMode: this.data.novel.rpgMode,
          mainCharacterId: this.data.novel.mainCharacterId,
          compendiumIds: this.data.novel.compendiumIds,
        }),
      );

      await this.copyCoverImage(createdNovel.id);

      await firstValueFrom(
        this.novelService.updateNovelProse(createdNovel.id, translatedProse),
      );

      this.toastrService.success('Translated novel created.');
      this.dialogRef.close({
        novelId: createdNovel.id,
      } as TranslateNovelDialogResult);
    } catch (error) {
      this.toastrService.error(
        error instanceof Error ? error.message : 'Failed to create translated novel.',
      );
    } finally {
      this.isSaving = false;
    }
  }

  get canAccept(): boolean {
    return (
      this.translatedProse !== null &&
      !this.isGenerating &&
      !this.isSaving &&
      this.formGroup.get('targetLanguage')?.value === this.translatedLanguage &&
      this.formGroup.get('targetLanguage')?.valid === true &&
      this.formGroup.get('title')?.valid === true &&
      this.formGroup.get('instructions')?.valid === true
    );
  }

  onPromptOptionsChanged(count: number): void {
    this.promptCount = count;
  }

  ngOnDestroy(): void {
    this.generationRequestId++;
    this.isGenerating = false;
  }

  get hasTranslationPromptOptions(): boolean {
    return this.promptCount > 0;
  }

  get canGenerate(): boolean {
    return (
      !this.isGenerating &&
      this.formGroup.get('targetLanguage')?.valid === true &&
      this.formGroup.get('title')?.valid === true &&
      this.formGroup.get('instructions')?.valid === true &&
      this.selectedPromptId !== null &&
      this.selectedModelId !== null
    );
  }

  private get selectedPromptId(): string | null {
    const formValue = this.formGroup.get('promptId')?.value;
    if (typeof formValue === 'string' && formValue.trim() !== '') {
      return formValue.trim();
    }

    const componentValue = this.promptSelect?.value;
    return componentValue && componentValue.trim() !== ''
      ? componentValue.trim()
      : null;
  }

  private get selectedModelId(): string | null {
    const formValue = this.formGroup.get('model')?.value;
    if (typeof formValue === 'string' && formValue.trim() !== '') {
      return formValue.trim();
    }

    const componentValue = this.modelSelect?.value;
    return componentValue && componentValue.trim() !== ''
      ? componentValue.trim()
      : null;
  }

  private parseTranslationResult(
    completion: GenerateTextCompletion,
  ): TranslationChapterResult | null {
    if (completion.parseError) {
      throw new Error(
        `Unable to read the streamed response: ${completion.parseError}`,
      );
    }

    try {
      const parsed = JSON.parse(completion.content.trim()) as TranslationChapterResult;
      if (
        !parsed ||
        typeof parsed.chapterTitle !== 'string' ||
        !Array.isArray(parsed.storyEvents) ||
        !Array.isArray(parsed.sections)
      ) {
        return null;
      }

      const storyEvents: StoryEvent[] = [];
      for (const event of parsed.storyEvents) {
        if (
          !event ||
          typeof event.title !== 'string' ||
          typeof event.date !== 'string' ||
          typeof event.description !== 'string'
        ) {
          return null;
        }

        storyEvents.push({
          title: event.title.trim(),
          date: event.date.trim(),
          description: event.description.trim(),
        });
      }

      const sectionIndexes = new Set<number>();
      const sections: TranslationSectionResult[] = [];
      for (const section of parsed.sections) {
        if (
          !section ||
          typeof section.sectionIndex !== 'number' ||
          !Number.isInteger(section.sectionIndex) ||
          section.sectionIndex < 0 ||
          sectionIndexes.has(section.sectionIndex) ||
          typeof section.summary !== 'string' ||
          typeof section.text !== 'string'
        ) {
          return null;
        }

        sectionIndexes.add(section.sectionIndex);
        sections.push({
          sectionIndex: section.sectionIndex,
          summary: section.summary.trim(),
          text: section.text.trim(),
        });
      }

      return {
        chapterTitle: parsed.chapterTitle.trim(),
        storyEvents,
        sections,
      };
    } catch {
      return null;
    }
  }

  private normalizeOptionalText(value: string | null): string | null {
    if (value === null) {
      return null;
    }

    const trimmed = value.trim();
    return trimmed.length > 0 ? trimmed : null;
  }

  private getDefaultTargetLanguage(): WritingLanguage {
    return this.writingLanguages.find(
      (language) => language !== this.data.novel.language,
    )!;
  }

  private async copyCoverImage(novelId: string): Promise<void> {
    const coverImageUrl = this.data.novel.coverImageUrl;
    if (!coverImageUrl) {
      return;
    }

    try {
      const response = await fetch(coverImageUrl);
      if (!response.ok) {
        return;
      }

      const blob = await response.blob();
      const coverFile = new File([blob], 'cover.png', {
        type: blob.type || 'image/png',
      });
      await firstValueFrom(
        this.novelService.uploadNovelCoverImage(novelId, coverFile),
      );
    } catch {
      // Cover copying is best-effort; the translated novel remains usable without it.
    }
  }
}
