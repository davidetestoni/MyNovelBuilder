import { TestBed } from '@angular/core/testing';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { of, Subject, throwError } from 'rxjs';
import {
  GenerateTextCompletion,
  GenerateTextService,
} from '../../services/generate-text.service';
import { LocalStorageService } from '../../services/local-storage.service';
import {
  GenerateTextRequestDto,
  NovelTextGenerationType,
} from '../../types/dtos/generate/generate-text-request.dto';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { PromptType } from '../../types/enums/prompt-type';
import { GenerateTextPreviewDialogService } from '../generate-text-preview/generate-text-preview-dialog.service';
import {
  GenerateStorySuggestionsDialogComponent,
  GenerateStorySuggestionsDialogData,
} from './generate-story-suggestions-dialog.component';

describe('GenerateStorySuggestionsDialogComponent workflow', () => {
  let component: GenerateStorySuggestionsDialogComponent;
  let generateTextService: jasmine.SpyObj<GenerateTextService>;
  let localStorageService: jasmine.SpyObj<LocalStorageService>;
  let previewDialogService: jasmine.SpyObj<GenerateTextPreviewDialogService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let config: { data: GenerateStorySuggestionsDialogData };

  const completion = (
    content: string,
    parseError: string | null = null,
    rawResponse = content,
  ): GenerateTextCompletion => ({ content, parseError, rawResponse });

  const setValidForm = (): void => {
    component.formGroup.setValue({
      promptId: '  prompt-one  ',
      model: '  model-one  ',
    });
  };

  beforeEach(() => {
    generateTextService = jasmine.createSpyObj<GenerateTextService>(
      'GenerateTextService',
      ['generateTextCompletion'],
    );
    localStorageService = jasmine.createSpyObj<LocalStorageService>(
      'LocalStorageService',
      ['setNestedStringForKey', 'pushNestedRecentStringForKey'],
    );
    previewDialogService = jasmine.createSpyObj<GenerateTextPreviewDialogService>(
      'GenerateTextPreviewDialogService',
      ['open'],
    );
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>('DynamicDialogRef', [
      'close',
    ]);
    config = {
      data: {
        prompts: [],
        novelId: 'novel-one',
        chapterIndex: 2,
        sectionIndex: 3,
        textOffset: 144,
      },
    };
    generateTextService.generateTextCompletion.and.returnValue(
      of(completion('[]')),
    );

    TestBed.configureTestingModule({
      providers: [
        { provide: GenerateTextService, useValue: generateTextService },
        { provide: LocalStorageService, useValue: localStorageService },
        {
          provide: GenerateTextPreviewDialogService,
          useValue: previewDialogService,
        },
        { provide: DynamicDialogConfig, useValue: config },
        { provide: DynamicDialogRef, useValue: dialogRef },
      ],
    });

    component = TestBed.runInInjectionContext(
      () => new GenerateStorySuggestionsDialogComponent(),
    );
  });

  it('starts with the supplied context and required generation fields', () => {
    expect(component.data).toBe(config.data);
    expect(component.promptType).toBe(PromptType.SuggestStoryDevelopments);
    expect(component.formGroup.getRawValue()).toEqual({
      promptId: '',
      model: '',
    });
    expect(component.formGroup.invalid).toBeTrue();
  });

  it('tracks available prompt options', () => {
    component.onPromptOptionsChanged(4);
    expect(component.promptCount).toBe(4);
  });

  it('does not generate with invalid or whitespace-only inputs', async () => {
    await component.generate();
    setValidForm();
    component.formGroup.controls.promptId.setValue('   ');
    await component.generate();

    expect(generateTextService.generateTextCompletion).not.toHaveBeenCalled();
    expect(localStorageService.setNestedStringForKey).not.toHaveBeenCalled();
  });

  it('sends normalized context and persists prompt and model recents', async () => {
    setValidForm();

    await component.generate();

    expect(localStorageService.setNestedStringForKey).toHaveBeenCalledOnceWith(
      LocalStorageKey.RecentPrompts,
      PromptType.SuggestStoryDevelopments,
      'prompt-one',
    );
    expect(
      localStorageService.pushNestedRecentStringForKey,
    ).toHaveBeenCalledOnceWith(
      LocalStorageKey.RecentTextModelsByContext,
      PromptType.SuggestStoryDevelopments,
      'model-one',
      5,
    );
    expect(
      generateTextService.generateTextCompletion,
    ).toHaveBeenCalledOnceWith({
      model: 'model-one',
      promptId: 'prompt-one',
      contextInfo: {
        $type: NovelTextGenerationType.SuggestStoryDevelopments,
        novelId: 'novel-one',
        chapterIndex: 2,
        sectionIndex: 3,
        textOffset: 144,
      },
    } as GenerateTextRequestDto);
  });

  it('previews the normalized request without generating', () => {
    setValidForm();

    component.previewPrompt();

    expect(previewDialogService.open).toHaveBeenCalledOnceWith({
      model: 'model-one',
      promptId: 'prompt-one',
      contextInfo: {
        $type: NovelTextGenerationType.SuggestStoryDevelopments,
        novelId: 'novel-one',
        chapterIndex: 2,
        sectionIndex: 3,
        textOffset: 144,
      },
    } as GenerateTextRequestDto);
    expect(generateTextService.generateTextCompletion).not.toHaveBeenCalled();
  });

  it('parses and trims generated suggestions', async () => {
    generateTextService.generateTextCompletion.and.returnValue(
      of(
        completion(
          JSON.stringify([
            {
              title: '  Follow the map  ',
              description: '  The party searches the ruins.  ',
            },
          ]),
        ),
      ),
    );
    setValidForm();

    await component.generate();

    expect(component.generatedSuggestions).toEqual([
      {
        title: 'Follow the map',
        description: 'The party searches the ruins.',
      },
    ]);
    expect(component.generationError).toBeNull();
    expect(component.rawOutput).toBeNull();
    expect(component.isGenerating).toBeFalse();
  });

  it('clears an earlier result before a new generation', async () => {
    component.generatedSuggestions = [
      { title: 'Old', description: 'Old result' },
    ];
    component.generationError = 'old error';
    component.rawOutput = 'old raw output';
    setValidForm();

    await component.generate();

    expect(component.generatedSuggestions).toEqual([]);
    expect(component.generationError).toBeNull();
    expect(component.rawOutput).toBeNull();
  });

  it('reports streamed decoder failures with raw output', async () => {
    generateTextService.generateTextCompletion.and.returnValue(
      of(completion('', 'incomplete line', 'raw stream')),
    );
    setValidForm();

    await component.generate();

    expect(component.generationError).toBe(
      'Unable to read the streamed response: incomplete line',
    );
    expect(component.rawOutput).toBe('raw stream');
    expect(component.generatedSuggestions).toEqual([]);
  });

  it('rejects malformed and structurally invalid output', async () => {
    setValidForm();
    for (const output of [
      'not-json',
      JSON.stringify({ title: 'object' }),
      JSON.stringify([{ title: 'Missing description' }]),
      JSON.stringify([null]),
    ]) {
      generateTextService.generateTextCompletion.and.returnValue(
        of(completion(output)),
      );

      await component.generate();

      expect(component.generationError).toBe(
        'The generated output is not valid JSON or does not match the expected format.',
      );
      expect(component.rawOutput).toBe(output);
    }
  });

  it('falls back to the raw response for empty invalid output', async () => {
    generateTextService.generateTextCompletion.and.returnValue(
      of(completion('   ', null, 'raw empty response')),
    );
    setValidForm();

    await component.generate();

    expect(component.rawOutput).toBe('raw empty response');
  });

  it('reports request errors and restores generation state', async () => {
    generateTextService.generateTextCompletion.and.returnValue(
      throwError(() => new Error('network down')),
    );
    setValidForm();

    await component.generate();

    expect(component.generationError).toBe(
      'Failed to generate story suggestions.',
    );
    expect(component.rawOutput).toBe('network down');
    expect(component.isGenerating).toBeFalse();
  });

  it('prevents duplicate generation while a request is pending', async () => {
    const response = new Subject<GenerateTextCompletion>();
    generateTextService.generateTextCompletion.and.returnValue(response);
    setValidForm();

    const generation = component.generate();
    await component.generate();

    expect(generateTextService.generateTextCompletion).toHaveBeenCalledTimes(1);
    response.next(completion('[]'));
    await generation;
  });

  it('ignores a late completion after destruction', async () => {
    const response = new Subject<GenerateTextCompletion>();
    generateTextService.generateTextCompletion.and.returnValue(response);
    setValidForm();

    const generation = component.generate();
    component.ngOnDestroy();
    response.next(
      completion('[{"title":"Late","description":"Too late"}]'),
    );
    await generation;

    expect(component.generatedSuggestions).toEqual([]);
    expect(component.isGenerating).toBeFalse();
  });

  it('closes with the selected description and normalized model', async () => {
    setValidForm();
    await component.generate();
    const suggestion = {
      title: 'Investigate',
      description: 'Visit the abandoned tower.',
    };

    component.selectSuggestion(suggestion);

    expect(dialogRef.close).toHaveBeenCalledOnceWith({
      instructions: 'Visit the abandoned tower.',
      model: 'model-one',
    });
  });

  it('does not select a suggestion while generation is pending', () => {
    component.isGenerating = true;

    component.selectSuggestion({
      title: 'Wait',
      description: 'This must not close.',
    });

    expect(dialogRef.close).not.toHaveBeenCalled();
  });
});
