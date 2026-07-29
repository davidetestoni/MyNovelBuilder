import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { of, Subject } from 'rxjs';
import {
  GenerateTextService,
  GenerateTextStreamUpdate,
} from '../../services/generate-text.service';
import {
  GenerateTextRequestDto,
  NovelTextGenerationType,
  SummarizeTextContextInfoDto,
} from '../../types/dtos/generate/generate-text-request.dto';
import {
  GenerateTextResultComponent,
  GenerateTextResultComponentData,
} from './generate-text-result.component';

describe('GenerateTextResultComponent workflow', () => {
  let component: GenerateTextResultComponent;
  let generateTextService: jasmine.SpyObj<GenerateTextService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let config: { data: GenerateTextResultComponentData };
  let generation: Subject<GenerateTextStreamUpdate>;

  const request = (): GenerateTextRequestDto => {
    const contextInfo: SummarizeTextContextInfoDto = {
      $type: NovelTextGenerationType.SummarizeText,
      novelId: 'novel-id',
      chapterIndex: 0,
      sectionIndex: 1,
    };

    return {
      promptId: 'prompt-id',
      model: 'model-a',
      contextInfo,
    };
  };

  beforeEach(() => {
    generation = new Subject<GenerateTextStreamUpdate>();
    generateTextService = jasmine.createSpyObj<GenerateTextService>(
      'GenerateTextService',
      ['generateText'],
    );
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>('DynamicDialogRef', [
      'close',
    ]);
    config = {
      data: {
        textToReplace: '<p>Old text</p>',
        request: request(),
      },
    };

    generateTextService.generateText.and.returnValue(generation);

    TestBed.configureTestingModule({
      providers: [
        { provide: GenerateTextService, useValue: generateTextService },
        { provide: DynamicDialogRef, useValue: dialogRef },
        { provide: DynamicDialogConfig, useValue: config },
      ],
    });

    component = TestBed.runInInjectionContext(
      () => new GenerateTextResultComponent(),
    );
  });

  it('starts generation on initialization with the configured request', () => {
    component.ngOnInit();

    expect(generateTextService.generateText).toHaveBeenCalledOnceWith(
      config.data.request,
    );
    expect(component.generatedText).toBe('[Generating text...]');
    expect(component.isGenerating).toBeTrue();
    expect(component.hasGenerationError).toBeFalse();
    expect(component.retryButtonLabel).toBe('Generating (0s)');

    component.ngOnDestroy();
  });

  it('applies non-empty streamed snapshots and ignores empty updates', () => {
    component.generateText();

    generation.next({ content: 'First snapshot', isComplete: false });
    expect(component.generatedText).toBe('First snapshot');

    generation.next({ content: '', isComplete: false });
    expect(component.generatedText).toBe('First snapshot');

    generation.next({ content: 'Final snapshot', isComplete: true });
    expect(component.generatedText).toBe('Final snapshot');
    expect(component.isGenerating).toBeFalse();
  });

  it('stops generation when the service completes without a completion update', () => {
    generateTextService.generateText.and.returnValue(
      of({ content: 'Completed text', isComplete: false }),
    );

    component.generateText();

    expect(component.generatedText).toBe('Completed text');
    expect(component.isGenerating).toBeFalse();
    expect(component.hasGenerationError).toBeFalse();
    expect(component.generationStatusLabel).toBe('Generation took 0s');
  });

  it('tracks elapsed time and reports the final duration', fakeAsync(() => {
    component.generateText();

    tick(2100);
    expect(component.generationElapsedSeconds).toBe(2);
    expect(component.retryButtonLabel).toBe('Generating (2s)');
    expect(component.generationStatusLabel).toBeNull();

    generation.next({ content: 'Done', isComplete: true });

    expect(component.generationElapsedSeconds).toBe(2);
    expect(component.lastGenerationDurationSeconds).toBe(2);
    expect(component.retryButtonLabel).toBe('Retry');
    expect(component.generationStatusLabel).toBe('Generation took 2s');
  }));

  it('reports errors, stops timing, and prevents accepting placeholder text', fakeAsync(() => {
    const error = new Error('generation failed');
    const consoleError = spyOn(console, 'error');
    component.generateText();
    tick(1100);

    generation.error(error);

    expect(consoleError).toHaveBeenCalledOnceWith(
      'Error generating text:',
      error,
    );
    expect(component.isGenerating).toBeFalse();
    expect(component.hasGenerationError).toBeTrue();
    expect(component.lastGenerationDurationSeconds).toBe(1);

    component.accept();
    expect(dialogRef.close).not.toHaveBeenCalled();
  }));

  it('cancels an older stream when retrying and resets transient state', () => {
    const firstGeneration = generation;
    const secondGeneration = new Subject<GenerateTextStreamUpdate>();
    generateTextService.generateText.and.returnValues(
      firstGeneration,
      secondGeneration,
    );

    component.generateText();
    component.hasGenerationError = true;
    component.lastGenerationDurationSeconds = 12;
    component.generateText();

    firstGeneration.next({ content: 'Stale result', isComplete: true });
    expect(component.generatedText).toBe('[Generating text...]');
    expect(component.isGenerating).toBeTrue();
    expect(component.hasGenerationError).toBeFalse();
    expect(component.lastGenerationDurationSeconds).toBeNull();

    secondGeneration.next({ content: 'Fresh result', isComplete: true });
    expect(component.generatedText).toBe('Fresh result');
    expect(component.isGenerating).toBeFalse();
    expect(generateTextService.generateText).toHaveBeenCalledTimes(2);
  });

  it('does not accept while generation is still running', () => {
    component.generateText();

    component.accept();

    expect(dialogRef.close).not.toHaveBeenCalled();
    component.ngOnDestroy();
  });

  it('accepts completed text after collapsing repeated line breaks', () => {
    component.generatedText = 'First\n\n\nSecond\nThird';
    component.isGenerating = false;

    component.accept();

    expect(dialogRef.close).toHaveBeenCalledOnceWith(
      'First\nSecond\nThird',
    );
  });

  it('accepts completed HTML without changing its markup', () => {
    component.generatedText = '<p>Generated <strong>text</strong></p>';
    component.isGenerating = false;

    component.accept();

    expect(dialogRef.close).toHaveBeenCalledOnceWith(
      '<p>Generated <strong>text</strong></p>',
    );
  });

  it('discards without returning a result', () => {
    component.discard();

    expect(dialogRef.close).toHaveBeenCalledOnceWith();
  });

  it('returns the back sentinel to reopen request configuration', () => {
    component.goBack();

    expect(dialogRef.close).toHaveBeenCalledOnceWith('back');
  });

  it('unsubscribes and stops the timer when destroyed', fakeAsync(() => {
    component.generateText();
    tick(1200);

    component.ngOnDestroy();
    generation.next({ content: 'Late update', isComplete: true });
    tick(2000);

    expect(component.generatedText).toBe('[Generating text...]');
    expect(component.generationElapsedSeconds).toBe(1);
    expect(component.lastGenerationDurationSeconds).toBe(1);
  }));
});
