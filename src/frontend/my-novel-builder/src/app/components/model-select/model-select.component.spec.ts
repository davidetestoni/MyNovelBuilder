import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { GenerateImageService } from '../../services/generate-image.service';
import { GenerateTextService } from '../../services/generate-text.service';
import { GenerateVideoService } from '../../services/generate-video.service';
import { LocalStorageService } from '../../services/local-storage.service';
import { ImageGenerationModelInfoDto } from '../../types/dtos/generate/image-generation-model-info.dto';
import { TextGenerationModelInfoDto } from '../../types/dtos/generate/text-generation-model-info.dto';
import { VideoGenerationModelInfoDto } from '../../types/dtos/generate/video-generation-model-info.dto';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { ModelSelectComponent } from './model-select.component';

describe('ModelSelectComponent', () => {
  let component: ModelSelectComponent;
  let generateTextService: jasmine.SpyObj<GenerateTextService>;
  let generateImageService: jasmine.SpyObj<GenerateImageService>;
  let generateVideoService: jasmine.SpyObj<GenerateVideoService>;
  let localStorageService: jasmine.SpyObj<LocalStorageService>;

  const textModel = (
    id: string,
    isVisionCapable = false,
    supportsStructuredOutputs = false,
  ): TextGenerationModelInfoDto => ({
    id,
    isVisionCapable,
    supportsStructuredOutputs,
    inputTokenPrice: 0,
    outputTokenPrice: 0,
  });

  const imageModel = (
    modelId: string,
    name: string,
    supportsImageGeneration: boolean,
    supportsImageEditing: boolean,
  ): ImageGenerationModelInfoDto => ({
    modelId,
    name,
    supportsImageGeneration,
    supportsImageEditing,
  });

  const videoModel = (
    modelId: string,
    name: string,
    supportsTextToVideo: boolean,
    supportsImageToVideo: boolean,
  ): VideoGenerationModelInfoDto => ({
    modelId,
    name,
    supportsTextToVideo,
    supportsImageToVideo,
  });

  beforeEach(() => {
    generateTextService = jasmine.createSpyObj<GenerateTextService>(
      'GenerateTextService',
      ['getAvailableModelInfos', 'sortModels'],
    );
    generateImageService = jasmine.createSpyObj<GenerateImageService>(
      'GenerateImageService',
      ['getAvailableModels'],
    );
    generateVideoService = jasmine.createSpyObj<GenerateVideoService>(
      'GenerateVideoService',
      ['getAvailableModels'],
    );
    localStorageService = jasmine.createSpyObj<LocalStorageService>(
      'LocalStorageService',
      [
        'getNestedStringArrayForKey',
        'getNestedStringForKey',
        'getStringForKey',
        'pushNestedRecentStringForKey',
      ],
    );

    generateTextService.getAvailableModelInfos.and.returnValue(of([]));
    generateTextService.sortModels.and.callFake((models) => [...models].sort());
    generateImageService.getAvailableModels.and.returnValue(of([]));
    generateVideoService.getAvailableModels.and.returnValue(of([]));
    localStorageService.getNestedStringArrayForKey.and.returnValue([]);
    localStorageService.getNestedStringForKey.and.returnValue(null);
    localStorageService.getStringForKey.and.returnValue(null);

    TestBed.configureTestingModule({
      providers: [
        { provide: GenerateTextService, useValue: generateTextService },
        { provide: GenerateImageService, useValue: generateImageService },
        { provide: GenerateVideoService, useValue: generateVideoService },
        { provide: LocalStorageService, useValue: localStorageService },
      ],
    });

    component = TestBed.runInInjectionContext(() => new ModelSelectComponent());
  });

  it('filters text models by capability before sorting them', () => {
    component.capability = 'vision';
    generateTextService.getAvailableModelInfos.and.returnValue(
      of([
        textModel('text-only'),
        textModel('vision-b', true),
        textModel('vision-a', true, true),
      ]),
    );

    component.ngOnInit();

    expect(generateTextService.sortModels).toHaveBeenCalledOnceWith([
      'vision-b',
      'vision-a',
    ]);
    expect(component.options).toEqual([
      { label: 'vision-a', value: 'vision-a' },
      { label: 'vision-b', value: 'vision-b' },
    ]);
    expect(component.value).toBe('vision-a');
  });

  it('prioritizes and selects a recent text model for its context', () => {
    component.storageContext = 'chapter';
    localStorageService.getNestedStringArrayForKey.and.returnValue([
      'model-b',
      'unavailable-model',
    ]);
    generateTextService.getAvailableModelInfos.and.returnValue(
      of([textModel('model-a'), textModel('model-b')]),
    );

    component.ngOnInit();

    expect(
      localStorageService.getNestedStringArrayForKey,
    ).toHaveBeenCalledWith(
      LocalStorageKey.RecentTextModelsByContext,
      'chapter',
    );
    expect(component.options.map((option) => option.value)).toEqual([
      'model-b',
      'model-a',
    ]);
    expect(component.value).toBe('model-b');
  });

  it('filters and deduplicates image-editing models', () => {
    component.capability = 'imageEdit';
    localStorageService.getNestedStringForKey.and.returnValue('edit-model');
    generateImageService.getAvailableModels.and.returnValue(
      of([
        imageModel('generate-only', 'Generate', true, false),
        imageModel('edit-model', 'Edit', false, true),
        imageModel('edit-model', 'Duplicate', true, true),
      ]),
    );

    component.ngOnInit();

    expect(component.options).toEqual([
      { label: 'Edit', value: 'edit-model' },
    ]);
    expect(component.value).toBe('edit-model');
    expect(localStorageService.getNestedStringForKey).toHaveBeenCalledWith(
      LocalStorageKey.LastImageModelByContext,
      'edit',
    );
  });

  it('filters image-to-video models and restores the legacy default', () => {
    component.capability = 'imageToVideo';
    localStorageService.getStringForKey.and.returnValue('image-video');
    generateVideoService.getAvailableModels.and.returnValue(
      of([
        videoModel('text-video', 'Text video', true, false),
        videoModel('image-video', 'Image video', false, true),
      ]),
    );

    component.ngOnInit();

    expect(component.options).toEqual([
      { label: 'Image video', value: 'image-video' },
    ]);
    expect(component.value).toBe('image-video');
    expect(localStorageService.getNestedStringForKey).toHaveBeenCalledWith(
      LocalStorageKey.LastVideoModelByContext,
      'generate-image-to-video',
    );
    expect(localStorageService.getStringForKey).toHaveBeenCalledWith(
      LocalStorageKey.LastVideoModel,
    );
  });

  it('persists text choices and notifies the form callbacks', () => {
    const onChange = jasmine.createSpy('onChange');
    const onTouched = jasmine.createSpy('onTouched');
    component.storageContext = 'scene';
    component.registerOnChange(onChange);
    component.registerOnTouched(onTouched);
    onChange.calls.reset();

    component.onValueChange('model-a');

    expect(
      localStorageService.pushNestedRecentStringForKey,
    ).toHaveBeenCalledOnceWith(
      LocalStorageKey.RecentTextModelsByContext,
      'scene',
      'model-a',
      5,
    );
    expect(onChange).toHaveBeenCalledOnceWith('model-a');
    expect(onTouched).toHaveBeenCalledTimes(1);
  });

  it('keeps valid form values and replaces invalid ones with the default', () => {
    generateTextService.getAvailableModelInfos.and.returnValue(
      of([textModel('model-a'), textModel('model-b')]),
    );
    component.ngOnInit();

    component.writeValue('model-b');
    expect(component.value).toBe('model-b');

    component.writeValue('missing-model');
    expect(component.value).toBe('model-a');
  });

  it('clears its loading state when loading models fails', () => {
    generateTextService.getAvailableModelInfos.and.returnValue(
      throwError(() => new Error('request failed')),
    );

    component.ngOnInit();

    expect(component.options).toEqual([]);
    expect(component.value).toBeNull();
    expect(component.isLoading).toBeFalse();
    expect(component.isDisabled).toBeFalse();
  });
});
