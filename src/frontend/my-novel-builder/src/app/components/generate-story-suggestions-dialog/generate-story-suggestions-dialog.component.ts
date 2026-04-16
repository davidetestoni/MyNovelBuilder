import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { ButtonModule } from 'primeng/button';
import { firstValueFrom } from 'rxjs';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { PromptType } from '../../types/enums/prompt-type';
import {
  GenerateTextRequestDto,
  NovelTextGenerationType,
  SuggestStoryDevelopmentsContextInfoDto,
} from '../../types/dtos/generate/generate-text-request.dto';
import {
  GenerateTextCompletion,
  GenerateTextService,
} from '../../services/generate-text.service';
import { LocalStorageService } from '../../services/local-storage.service';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { PromptSelectComponent } from '../prompt-select/prompt-select.component';
import { ModelSelectComponent } from '../model-select/model-select.component';

interface StoryDevelopmentSuggestion {
  title: string;
  description: string;
}

export interface GenerateStorySuggestionsDialogData {
  prompts: PromptDto[];
  novelId: string;
  chapterIndex: number;
  sectionIndex: number;
  textOffset: number;
}

export interface GenerateStorySuggestionsDialogResult {
  instructions: string;
  model: string;
}

@Component({
  selector: 'app-generate-story-suggestions-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    ButtonModule,
    PromptSelectComponent,
    ModelSelectComponent,
  ],
  templateUrl: './generate-story-suggestions-dialog.component.html',
  styleUrl: './generate-story-suggestions-dialog.component.scss',
})
export class GenerateStorySuggestionsDialogComponent {
  config = inject(DynamicDialogConfig);
  dialogRef = inject(DynamicDialogRef);

  readonly generateTextService = inject(GenerateTextService);
  readonly localStorageService = inject(LocalStorageService);
  readonly promptType = PromptType.SuggestStoryDevelopments;

  data: GenerateStorySuggestionsDialogData;
  promptCount = 0;
  generatedSuggestions: StoryDevelopmentSuggestion[] = [];
  isGenerating = false;
  generationError: string | null = null;
  rawOutput: string | null = null;

  formGroup = new FormGroup({
    promptId: new FormControl('', [Validators.required]),
    model: new FormControl('', [Validators.required]),
  });

  constructor() {
    this.data = this.config.data as GenerateStorySuggestionsDialogData;
  }

  async generate(): Promise<void> {
    if (this.formGroup.invalid) {
      return;
    }

    const promptId = this.formGroup.get('promptId')!.value ?? '';
    const model = this.formGroup.get('model')!.value ?? '';

    this.localStorageService.setNestedStringForKey(
      LocalStorageKey.RecentPrompts,
      this.promptType,
      promptId,
    );
    this.saveRecentModelForContext(model);

    this.isGenerating = true;
    this.generationError = null;
    this.generatedSuggestions = [];
    this.rawOutput = null;

    const request: GenerateTextRequestDto = {
      model,
      promptId,
      contextInfo: <SuggestStoryDevelopmentsContextInfoDto>{
        $type: NovelTextGenerationType.SuggestStoryDevelopments,
        novelId: this.data.novelId,
        chapterIndex: this.data.chapterIndex,
        sectionIndex: this.data.sectionIndex,
        textOffset: this.data.textOffset,
      },
    };

    let completion: GenerateTextCompletion;
    try {
      completion = await firstValueFrom(
        this.generateTextService.generateTextCompletion(request),
      );
    } catch (error) {
      this.generationError = 'Failed to generate story suggestions.';
      this.rawOutput = error instanceof Error ? error.message : null;
      this.isGenerating = false;
      return;
    }

    if (completion.parseError) {
      this.generationError = `Unable to read the streamed response: ${completion.parseError}`;
      this.rawOutput = completion.rawResponse || null;
      this.isGenerating = false;
      return;
    }

    const rawOutput = completion.content.trim();
    const parsedSuggestions = this.parseSuggestions(rawOutput);

    if (parsedSuggestions === null) {
      this.generationError =
        'The generated output is not valid JSON or does not match the expected format.';
      this.rawOutput = rawOutput || completion.rawResponse || null;
      this.isGenerating = false;
      return;
    }

    this.generatedSuggestions = parsedSuggestions;
    this.isGenerating = false;
  }

  selectSuggestion(suggestion: StoryDevelopmentSuggestion): void {
    if (this.isGenerating) {
      return;
    }

    this.dialogRef.close({
      instructions: suggestion.description,
      model: this.formGroup.get('model')!.value ?? '',
    } as GenerateStorySuggestionsDialogResult);
  }

  onPromptOptionsChanged(count: number): void {
    this.promptCount = count;
  }

  private parseSuggestions(
    rawOutput: string,
  ): StoryDevelopmentSuggestion[] | null {
    if (!rawOutput) {
      return null;
    }

    try {
      const parsed = JSON.parse(rawOutput) as unknown;
      if (!Array.isArray(parsed)) {
        return null;
      }

      const suggestions: StoryDevelopmentSuggestion[] = [];
      for (const item of parsed) {
        if (
          !item ||
          typeof item !== 'object' ||
          typeof item.title !== 'string' ||
          typeof item.description !== 'string'
        ) {
          return null;
        }

        suggestions.push({
          title: item.title.trim(),
          description: item.description.trim(),
        });
      }

      return suggestions;
    } catch {
      return null;
    }
  }

  private saveRecentModelForContext(model: string): void {
    if (!model.trim()) {
      return;
    }

    this.localStorageService.pushNestedRecentStringForKey(
      LocalStorageKey.RecentTextModelsByContext,
      this.promptType,
      model,
      5,
    );
  }
}
