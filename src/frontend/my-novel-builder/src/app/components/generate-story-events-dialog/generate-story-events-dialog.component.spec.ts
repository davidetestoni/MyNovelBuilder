import { TestBed } from '@angular/core/testing';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { Observable, of, Subject, throwError } from 'rxjs';
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
import {
  GenerateStoryEventsDialogComponent,
  GenerateStoryEventsDialogData,
} from './generate-story-events-dialog.component';

describe('GenerateStoryEventsDialogComponent workflow', () => {
  let component: GenerateStoryEventsDialogComponent;
  let generateTextService: jasmine.SpyObj<GenerateTextService>;
  let localStorageService: jasmine.SpyObj<LocalStorageService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let config: { data: GenerateStoryEventsDialogData };

  const completion = (
    content: string,
    parseError: string | null = null,
    rawResponse = content,
  ): GenerateTextCompletion => ({ content, parseError, rawResponse });

  const setValidForm = (): void => {
    component.formGroup.setValue({
      chapterIndex: 1,
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
      ['setNestedStringForKey'],
    );
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>('DynamicDialogRef', [
      'close',
    ]);
    config = {
      data: {
        chapters: [
          { label: 'Opening', value: 0 },
          { label: 'Reckoning', value: 1 },
        ],
        selectedChapterIndex: 1,
        prompts: [],
        novelId: 'novel-one',
      },
    };
    generateTextService.generateTextCompletion.and.returnValue(
      of(completion('[]')),
    );

    TestBed.configureTestingModule({
      providers: [
        { provide: GenerateTextService, useValue: generateTextService },
        { provide: LocalStorageService, useValue: localStorageService },
        { provide: DynamicDialogConfig, useValue: config },
        { provide: DynamicDialogRef, useValue: dialogRef },
      ],
    });

    component = TestBed.runInInjectionContext(
      () => new GenerateStoryEventsDialogComponent(),
    );
  });

  it('starts on the supplied chapter with required generation fields', () => {
    expect(component.data).toBe(config.data);
    expect(component.formGroup.getRawValue()).toEqual({
      chapterIndex: 1,
      promptId: '',
      model: '',
    });
    expect(component.formGroup.invalid).toBeTrue();
    expect(component.PromptType).toBe(PromptType);
  });

  it('tracks the number of matching prompt options', () => {
    component.onPromptOptionsChanged(3);
    expect(component.promptCount).toBe(3);
  });

  it('does not generate with invalid or whitespace-only inputs', async () => {
    await component.generate();
    setValidForm();
    component.formGroup.controls.model.setValue('   ');
    await component.generate();

    expect(generateTextService.generateTextCompletion).not.toHaveBeenCalled();
    expect(localStorageService.setNestedStringForKey).not.toHaveBeenCalled();
  });

  it('sends a normalized request and stores the selected prompt', async () => {
    setValidForm();

    await component.generate();

    expect(localStorageService.setNestedStringForKey).toHaveBeenCalledOnceWith(
      LocalStorageKey.RecentPrompts,
      PromptType.CreateStoryEvents,
      'prompt-one',
    );
    expect(
      generateTextService.generateTextCompletion,
    ).toHaveBeenCalledOnceWith({
      model: 'model-one',
      promptId: 'prompt-one',
      contextInfo: {
        $type: NovelTextGenerationType.CreateStoryEvents,
        novelId: 'novel-one',
        chapterIndex: 1,
      },
    } as GenerateTextRequestDto);
  });

  it('parses and trims a generated story-event array', async () => {
    generateTextService.generateTextCompletion.and.returnValue(
      of(
        completion(
          JSON.stringify([
            {
              title: '  Discovery  ',
              date: '  Dawn  ',
              description: '  A hidden door opens.  ',
            },
          ]),
        ),
      ),
    );
    setValidForm();

    await component.generate();

    expect(component.generatedPreviews).toEqual([
      {
        chapterIndex: 1,
        chapterTitle: 'Reckoning',
        storyEvents: [
          {
            title: 'Discovery',
            date: 'Dawn',
            description: 'A hidden door opens.',
          },
        ],
        error: null,
        rawOutput: null,
      },
    ]);
    expect(component.canAccept).toBeTrue();
    expect(component.isGenerating).toBeFalse();
  });

  it('uses a fallback chapter title when the selection is not in the labels', async () => {
    config.data.selectedChapterIndex = 4;
    component = TestBed.runInInjectionContext(
      () => new GenerateStoryEventsDialogComponent(),
    );
    component.formGroup.patchValue({
      promptId: 'prompt',
      model: 'model',
    });

    await component.generate();

    expect(component.generatedPreviews[0].chapterTitle).toBe('Chapter 5');
  });

  it('reports decoder failures with the raw streamed response', async () => {
    generateTextService.generateTextCompletion.and.returnValue(
      of(completion('', 'bad NDJSON', 'raw chunks')),
    );
    setValidForm();

    await component.generate();

    expect(component.generatedPreviews[0]).toEqual(
      jasmine.objectContaining({
        storyEvents: [],
        error: 'Unable to read the streamed response: bad NDJSON',
        rawOutput: 'raw chunks',
      }),
    );
    expect(component.canAccept).toBeFalse();
  });

  it('rejects malformed JSON and preserves it for diagnosis', async () => {
    generateTextService.generateTextCompletion.and.returnValue(
      of(completion('{not-json')),
    );
    setValidForm();

    await component.generate();

    expect(component.generatedPreviews[0]).toEqual(
      jasmine.objectContaining({
        storyEvents: [],
        error:
          'The generated output is not valid JSON or does not match the expected format.',
        rawOutput: '{not-json',
      }),
    );
  });

  it('rejects non-array and structurally invalid generated data', async () => {
    setValidForm();
    for (const output of [
      JSON.stringify({ title: 'Not an array' }),
      JSON.stringify([{ title: 'Missing fields' }]),
      JSON.stringify([null]),
    ]) {
      generateTextService.generateTextCompletion.and.returnValue(
        of(completion(output)),
      );

      await component.generate();

      expect(component.generatedPreviews[0].storyEvents).toEqual([]);
      expect(component.generatedPreviews[0].error).not.toBeNull();
    }
  });

  it('reports request failures and exposes Error details', async () => {
    generateTextService.generateTextCompletion.and.returnValue(
      throwError(() => new Error('provider offline')),
    );
    setValidForm();

    await component.generate();

    expect(component.generatedPreviews[0]).toEqual(
      jasmine.objectContaining({
        error: 'Failed to generate story events.',
        rawOutput: 'provider offline',
      }),
    );
    expect(component.isGenerating).toBeFalse();
  });

  it('prevents duplicate generation while a request is pending', async () => {
    const response = new Subject<GenerateTextCompletion>();
    generateTextService.generateTextCompletion.and.returnValue(response);
    setValidForm();

    const generation = component.generate();
    await component.generate();

    expect(generateTextService.generateTextCompletion).toHaveBeenCalledTimes(1);
    expect(component.isGenerating).toBeTrue();
    response.next(completion('[]'));
    await generation;
    expect(component.isGenerating).toBeFalse();
  });

  it('ignores a late completion after destruction', async () => {
    const response = new Subject<GenerateTextCompletion>();
    generateTextService.generateTextCompletion.and.returnValue(response);
    setValidForm();

    const generation = component.generate();
    component.ngOnDestroy();
    response.next(
      completion(
        '[{"title":"Late","date":"","description":"Too late"}]',
      ),
    );
    await generation;

    expect(component.generatedPreviews).toEqual([]);
    expect(component.isGenerating).toBeFalse();
  });

  it('accepts only previews containing events', () => {
    component.generatedPreviews = [
      {
        chapterIndex: 0,
        chapterTitle: 'Opening',
        storyEvents: [],
        error: 'failed',
        rawOutput: null,
      },
      {
        chapterIndex: 1,
        chapterTitle: 'Reckoning',
        storyEvents: [
          { title: 'Event', date: '', description: 'Description' },
        ],
        error: null,
        rawOutput: null,
      },
    ];

    component.accept();

    expect(dialogRef.close).toHaveBeenCalledOnceWith({
      chapters: [
        {
          chapterIndex: 1,
          storyEvents: [
            { title: 'Event', date: '', description: 'Description' },
          ],
        },
      ],
    });
  });

  it('does not accept without events or while generating', () => {
    component.accept();
    component.generatedPreviews = [
      {
        chapterIndex: 0,
        chapterTitle: 'Opening',
        storyEvents: [
          { title: 'Event', date: '', description: 'Description' },
        ],
        error: null,
        rawOutput: null,
      },
    ];
    component.isGenerating = true;
    component.accept();

    expect(dialogRef.close).not.toHaveBeenCalled();
    expect(component.canAccept).toBeFalse();
  });
});
