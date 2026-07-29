import { TestBed } from '@angular/core/testing';
import {
  DialogService,
  DynamicDialogConfig,
  DynamicDialogRef,
} from 'primeng/dynamicdialog';
import { LocalStorageService } from '../../services/local-storage.service';
import {
  GenerateTextContextInfoDto,
  NovelTextGenerationType,
  SummarizeTextContextInfoDto,
} from '../../types/dtos/generate/generate-text-request.dto';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { PromptType } from '../../types/enums/prompt-type';
import { GenerateTextPreviewComponent } from '../generate-text-preview/generate-text-preview.component';
import {
  GenerateTextComponent,
  GenerateTextComponentData,
} from './generate-text.component';

describe('GenerateTextComponent workflow', () => {
  let component: GenerateTextComponent;
  let localStorageService: jasmine.SpyObj<LocalStorageService>;
  let dialogService: jasmine.SpyObj<DialogService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let config: { data: GenerateTextComponentData };

  const prompt = (id: string, name: string): PromptDto => ({
    id,
    name,
    type: PromptType.GenerateText,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    messages: [],
  });

  const contextInfo = (): GenerateTextContextInfoDto => ({
    $type: NovelTextGenerationType.GenerateText,
    novelId: 'novel-id',
    chapterIndex: 1,
    sectionIndex: 2,
    textOffset: 42,
    instructions: 'Original instructions',
  });

  const summaryContextInfo = (): SummarizeTextContextInfoDto => ({
    $type: NovelTextGenerationType.SummarizeText,
    novelId: 'novel-id',
    chapterIndex: 0,
    sectionIndex: 1,
  });

  const data = (
    overrides: Partial<GenerateTextComponentData> = {},
  ): GenerateTextComponentData => ({
    prompts: [prompt('prompt-a', 'Prompt A'), prompt('prompt-b', 'Prompt B')],
    contextInfo: contextInfo(),
    instructionsRequired: false,
    showInstructions: true,
    storageContext: 'scene',
    ...overrides,
  });

  const createComponent = (
    componentData: GenerateTextComponentData = data(),
  ): GenerateTextComponent => {
    config.data = componentData;
    return TestBed.runInInjectionContext(() => new GenerateTextComponent());
  };

  beforeEach(() => {
    localStorageService = jasmine.createSpyObj<LocalStorageService>(
      'LocalStorageService',
      [
        'getNestedStringForKey',
        'setNestedStringForKey',
        'pushNestedRecentStringForKey',
      ],
    );
    dialogService = jasmine.createSpyObj<DialogService>('DialogService', [
      'open',
    ]);
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>('DynamicDialogRef', [
      'close',
    ]);
    config = { data: data() };

    localStorageService.getNestedStringForKey.and.returnValue(null);
    dialogService.open.and.returnValue({} as DynamicDialogRef);

    TestBed.configureTestingModule({
      providers: [
        { provide: LocalStorageService, useValue: localStorageService },
        { provide: DialogService, useValue: dialogService },
        { provide: DynamicDialogRef, useValue: dialogRef },
        { provide: DynamicDialogConfig, useValue: config },
      ],
    });

    component = createComponent();
  });

  it('rejects a dialog with no available prompts', () => {
    expect(() => createComponent(data({ prompts: [] }))).toThrowError(
      'No prompts provided',
    );
  });

  it('defaults to the first prompt and restores recent values', () => {
    localStorageService.getNestedStringForKey.and.callFake((key) => {
      if (key === LocalStorageKey.RecentInstructions) {
        return 'Remembered instructions';
      }
      if (key === LocalStorageKey.RecentPrompts) {
        return 'prompt-b';
      }
      return null;
    });

    component = createComponent();

    expect(component.formGroup.getRawValue()).toEqual({
      promptId: 'prompt-b',
      model: '',
      instructions: 'Remembered instructions',
    });
    expect(component.storageContext).toBe('scene');
    expect(localStorageService.getNestedStringForKey).toHaveBeenCalledWith(
      LocalStorageKey.RecentInstructions,
      PromptType.GenerateText,
    );
    expect(localStorageService.getNestedStringForKey).toHaveBeenCalledWith(
      LocalStorageKey.RecentPrompts,
      PromptType.GenerateText,
    );
  });

  it('falls back to the prompt type for model-storage context', () => {
    component = createComponent(data({ storageContext: undefined }));

    expect(component.storageContext).toBe(PromptType.GenerateText);
  });

  it('lets explicit initial values override restored values', () => {
    localStorageService.getNestedStringForKey.and.returnValue('stored');

    component = createComponent(
      data({
        initialPromptId: 'prompt-a',
        initialModel: 'model-a',
        initialInstructions: 'Initial instructions',
      }),
    );

    expect(component.formGroup.getRawValue()).toEqual({
      promptId: 'prompt-a',
      model: 'model-a',
      instructions: 'Initial instructions',
    });
  });

  it('requires visible instructions only when configured', () => {
    component = createComponent(
      data({ instructionsRequired: true, initialModel: 'model-a' }),
    );

    expect(component.showInstructions).toBeTrue();
    expect(component.formGroup.controls.instructions.enabled).toBeTrue();
    expect(
      component.formGroup.controls.instructions.hasError('required'),
    ).toBeTrue();

    component.formGroup.controls.instructions.setValue('Use close POV');
    expect(component.formGroup.valid).toBeTrue();
  });

  it('disables hidden instructions without making the form invalid', () => {
    component = createComponent(
      data({
        instructionsRequired: true,
        showInstructions: false,
        initialModel: 'model-a',
      }),
    );

    expect(component.showInstructions).toBeFalse();
    expect(component.formGroup.controls.instructions.disabled).toBeTrue();
    expect(component.formGroup.valid).toBeTrue();
  });

  it('does not submit or persist an invalid form', () => {
    component.accept();

    expect(dialogRef.close).not.toHaveBeenCalled();
    expect(localStorageService.setNestedStringForKey).not.toHaveBeenCalled();
    expect(
      localStorageService.pushNestedRecentStringForKey,
    ).not.toHaveBeenCalled();
  });

  it('maps the form into an immutable request and persists recent choices', () => {
    const originalContext = contextInfo();
    component = createComponent(data({ contextInfo: originalContext }));
    component.formGroup.setValue({
      promptId: 'prompt-b',
      model: 'model-a',
      instructions: 'Focus on atmosphere',
    });

    component.accept();

    expect(dialogRef.close).toHaveBeenCalledOnceWith({
      promptId: 'prompt-b',
      model: 'model-a',
      contextInfo: {
        ...originalContext,
        instructions: 'Focus on atmosphere',
      },
    });
    expect(originalContext.instructions).toBe('Original instructions');
    expect(
      localStorageService.pushNestedRecentStringForKey,
    ).toHaveBeenCalledOnceWith(
      LocalStorageKey.RecentTextModelsByContext,
      'scene',
      'model-a',
      5,
    );
    expect(localStorageService.setNestedStringForKey).toHaveBeenCalledWith(
      LocalStorageKey.RecentInstructions,
      PromptType.GenerateText,
      'Focus on atmosphere',
    );
    expect(localStorageService.setNestedStringForKey).toHaveBeenCalledWith(
      LocalStorageKey.RecentPrompts,
      PromptType.GenerateText,
      'prompt-b',
    );
  });

  it('preserves context types that do not contain instructions', () => {
    component = createComponent(
      data({
        contextInfo: summaryContextInfo(),
      }),
    );
    component.formGroup.setValue({
      promptId: 'prompt-a',
      model: 'model-a',
      instructions: 'Ignored by this context',
    });

    component.accept();

    expect(dialogRef.close).toHaveBeenCalledOnceWith({
      promptId: 'prompt-a',
      model: 'model-a',
      contextInfo: {
        $type: NovelTextGenerationType.SummarizeText,
        novelId: 'novel-id',
        chapterIndex: 0,
        sectionIndex: 1,
      },
    });
  });

  it('does not persist nullable instructions or a model without a storage context', () => {
    component = createComponent(data({ storageContext: '' }));
    component.formGroup.setValue({
      promptId: 'prompt-a',
      model: 'model-a',
      instructions: null,
    });

    component.accept();

    expect(
      localStorageService.pushNestedRecentStringForKey,
    ).not.toHaveBeenCalled();
    expect(localStorageService.setNestedStringForKey).not.toHaveBeenCalledWith(
      LocalStorageKey.RecentInstructions,
      jasmine.any(String),
      jasmine.any(String),
    );
  });

  it('does not open a preview for an invalid form', () => {
    component.openPreviewDialog();

    expect(dialogService.open).not.toHaveBeenCalled();
  });

  it('opens a preview with the same normalized request and saves its model', () => {
    component.formGroup.setValue({
      promptId: 'prompt-b',
      model: 'model-a',
      instructions: 'Preview this',
    });

    component.openPreviewDialog();

    expect(dialogService.open).toHaveBeenCalledOnceWith(
      GenerateTextPreviewComponent,
      jasmine.objectContaining({
        header: 'Prompt Preview',
        width: '50vw',
        modal: true,
        data: {
          request: {
            promptId: 'prompt-b',
            model: 'model-a',
            contextInfo: {
              ...contextInfo(),
              instructions: 'Preview this',
            },
          },
        },
      }),
    );
    expect(
      localStorageService.pushNestedRecentStringForKey,
    ).toHaveBeenCalledOnceWith(
      LocalStorageKey.RecentTextModelsByContext,
      'scene',
      'model-a',
      5,
    );
    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('resolves prompt names and returns an empty fallback', () => {
    expect(component.getPromptName('prompt-b')).toBe('Prompt B');
    expect(component.getPromptName('missing')).toBe('');
  });
});
