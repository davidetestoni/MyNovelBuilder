import { TestBed } from '@angular/core/testing';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { of, Subject, throwError } from 'rxjs';
import { CompendiumService } from '../../services/compendium.service';
import { GenerateTextService } from '../../services/generate-text.service';
import { CompendiumRecordDto } from '../../types/dtos/compendium-record/compendium-record.dto';
import {
  GenerateTextRequestDto,
  NovelTextGenerationType,
  SummarizeTextContextInfoDto,
} from '../../types/dtos/generate/generate-text-request.dto';
import { TextGenerationModelInfoDto } from '../../types/dtos/generate/text-generation-model-info.dto';
import { TextGenerationPreviewDto } from '../../types/dtos/generate/text-generation-preview.dto';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { PromptMessageRole } from '../../types/enums/prompt-message-role';
import {
  GenerateTextPreviewComponent,
  GenerateTextPreviewComponentData,
} from './generate-text-preview.component';

describe('GenerateTextPreviewComponent workflow', () => {
  let component: GenerateTextPreviewComponent;
  let generateTextService: jasmine.SpyObj<GenerateTextService>;
  let compendiumService: jasmine.SpyObj<CompendiumService>;
  let config: { data: GenerateTextPreviewComponentData };

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

  const preview = (
    includedCompendiumRecordIds: string[] = [],
  ): TextGenerationPreviewDto => ({
    inputTokens: 1250,
    includedCompendiumRecordIds,
    finalMessages: [
      {
        role: PromptMessageRole.System,
        message: 'System message',
      },
      {
        role: PromptMessageRole.User,
        message: 'User message',
      },
    ],
  });

  const modelInfo = (
    id: string,
    inputTokenPrice = 0.000002,
  ): TextGenerationModelInfoDto => ({
    id,
    isVisionCapable: false,
    supportsStructuredOutputs: false,
    inputTokenPrice,
    outputTokenPrice: 0.000004,
  });

  const record = (
    id: string,
    name: string,
    type: CompendiumRecordType,
  ): CompendiumRecordDto => ({
    id,
    name,
    type,
    aliases: '',
    context: '',
    contextTokenCount: 0,
    media: [],
    compendiumId: 'compendium-id',
    alwaysIncluded: false,
    characterVoiceAssignments: [],
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  });

  beforeEach(() => {
    generateTextService = jasmine.createSpyObj<GenerateTextService>(
      'GenerateTextService',
      ['getGenerationPreview', 'getAvailableModelInfos'],
    );
    compendiumService = jasmine.createSpyObj<CompendiumService>(
      'CompendiumService',
      ['getRecordsByIds'],
    );
    config = { data: { request: request() } };

    generateTextService.getGenerationPreview.and.returnValue(of(preview()));
    generateTextService.getAvailableModelInfos.and.returnValue(
      of([modelInfo('model-a')]),
    );
    compendiumService.getRecordsByIds.and.returnValue(of([]));

    TestBed.configureTestingModule({
      providers: [
        { provide: GenerateTextService, useValue: generateTextService },
        { provide: CompendiumService, useValue: compendiumService },
        { provide: DynamicDialogConfig, useValue: config },
        { provide: DynamicDialogRef, useValue: {} },
      ],
    });

    component = TestBed.runInInjectionContext(
      () => new GenerateTextPreviewComponent(),
    );
  });

  it('loads the preview and model information for the configured request', () => {
    const loadedPreview = preview();
    generateTextService.getGenerationPreview.and.returnValue(of(loadedPreview));

    component.ngOnInit();

    expect(generateTextService.getGenerationPreview).toHaveBeenCalledOnceWith(
      config.data.request,
    );
    expect(generateTextService.getAvailableModelInfos).toHaveBeenCalledTimes(1);
    expect(component.preview).toBe(loadedPreview);
    expect(component.selectedModelInfo).toEqual(modelInfo('model-a'));
    expect(component.isLoading).toBeFalse();
    expect(component.hasError).toBeFalse();
  });

  it('waits for both preview requests before updating state', () => {
    const previewResponse = new Subject<TextGenerationPreviewDto>();
    const modelsResponse = new Subject<TextGenerationModelInfoDto[]>();
    generateTextService.getGenerationPreview.and.returnValue(previewResponse);
    generateTextService.getAvailableModelInfos.and.returnValue(modelsResponse);

    component.ngOnInit();
    previewResponse.next(preview());
    previewResponse.complete();

    expect(component.preview).toBeNull();
    expect(component.isLoading).toBeTrue();

    modelsResponse.next([modelInfo('model-a')]);
    modelsResponse.complete();

    expect(component.preview).toEqual(preview());
    expect(component.isLoading).toBeFalse();
  });

  it('calculates the estimated input price from the selected model', () => {
    generateTextService.getGenerationPreview.and.returnValue(
      of({ ...preview(), inputTokens: 2000 }),
    );
    generateTextService.getAvailableModelInfos.and.returnValue(
      of([modelInfo('other'), modelInfo('model-a', 0.000003)]),
    );

    component.ngOnInit();

    expect(component.estimatedInputPrice).toBeCloseTo(0.006, 10);
  });

  it('leaves pricing unavailable when the requested model is absent', () => {
    generateTextService.getAvailableModelInfos.and.returnValue(
      of([modelInfo('other')]),
    );

    component.ngOnInit();

    expect(component.selectedModelInfo).toBeNull();
    expect(component.estimatedInputPrice).toBeNull();
    expect(component.isLoading).toBeFalse();
  });

  it('skips record loading when the preview contains no record ids', () => {
    component.ngOnInit();

    expect(compendiumService.getRecordsByIds).not.toHaveBeenCalled();
    expect(component.includedRecords).toEqual([]);
    expect(component.isLoading).toBeFalse();
  });

  it('loads and deterministically sorts included records without mutating them', () => {
    const records = [
      record('place', 'Zeta', CompendiumRecordType.Place),
      record('character-z', 'zoe', CompendiumRecordType.Character),
      record('other', 'Other', CompendiumRecordType.Other),
      record('character-a', 'Ada', CompendiumRecordType.Character),
      record('object', 'Object', CompendiumRecordType.Object),
    ];
    const originalOrder = [...records];
    generateTextService.getGenerationPreview.and.returnValue(
      of(preview(records.map(({ id }) => id))),
    );
    compendiumService.getRecordsByIds.and.returnValue(of(records));

    component.ngOnInit();

    expect(compendiumService.getRecordsByIds).toHaveBeenCalledOnceWith([
      'place',
      'character-z',
      'other',
      'character-a',
      'object',
    ]);
    expect(component.includedRecords.map(({ id }) => id)).toEqual([
      'character-a',
      'character-z',
      'place',
      'object',
      'other',
    ]);
    expect(records).toEqual(originalOrder);
    expect(component.isLoading).toBeFalse();
  });

  it('returns the first current record image or null', () => {
    const target = record(
      'record-id',
      'Record',
      CompendiumRecordType.Character,
    );
    target.media = [
      {
        id: 'old',
        url: '/old.png',
        isCurrent: false,
        isVideo: false,
      },
      {
        id: 'current',
        url: '/current.png',
        isCurrent: true,
        isVideo: false,
      },
      {
        id: 'second-current',
        url: '/second.png',
        isCurrent: true,
        isVideo: false,
      },
    ];

    expect(component.getRecordImage(target)).toBe('/current.png');

    target.media.forEach((media) => (media.isCurrent = false));
    expect(component.getRecordImage(target)).toBeNull();
  });

  it('reports preview-loading failures and stops loading', () => {
    generateTextService.getGenerationPreview.and.returnValue(
      throwError(() => new Error('preview failed')),
    );

    component.ngOnInit();

    expect(component.hasError).toBeTrue();
    expect(component.isLoading).toBeFalse();
    expect(component.preview).toBeNull();
    expect(compendiumService.getRecordsByIds).not.toHaveBeenCalled();
  });

  it('reports model-loading failures and stops loading', () => {
    generateTextService.getAvailableModelInfos.and.returnValue(
      throwError(() => new Error('models failed')),
    );

    component.ngOnInit();

    expect(component.hasError).toBeTrue();
    expect(component.isLoading).toBeFalse();
    expect(component.preview).toBeNull();
  });

  it('reports included-record failures and clears the loading state', () => {
    generateTextService.getGenerationPreview.and.returnValue(
      of(preview(['record-id'])),
    );
    compendiumService.getRecordsByIds.and.returnValue(
      throwError(() => new Error('records failed')),
    );

    component.ngOnInit();

    expect(component.preview).toEqual(preview(['record-id']));
    expect(component.hasError).toBeTrue();
    expect(component.isLoading).toBeFalse();
    expect(component.includedRecords).toEqual([]);
  });

  it('keeps loading until the included-record request completes', () => {
    const recordsResponse = new Subject<CompendiumRecordDto[]>();
    const includedRecord = record(
      'record-id',
      'Record',
      CompendiumRecordType.Character,
    );
    generateTextService.getGenerationPreview.and.returnValue(
      of(preview(['record-id'])),
    );
    compendiumService.getRecordsByIds.and.returnValue(recordsResponse);

    component.ngOnInit();

    expect(component.isLoading).toBeTrue();
    recordsResponse.next([includedRecord]);
    expect(component.includedRecords).toEqual([includedRecord]);
    expect(component.isLoading).toBeTrue();

    recordsResponse.complete();
    expect(component.isLoading).toBeFalse();
  });
});
