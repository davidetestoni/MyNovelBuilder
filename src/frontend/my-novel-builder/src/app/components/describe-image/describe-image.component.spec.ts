import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { TestBed } from '@angular/core/testing';
import { ToastrService } from 'ngx-toastr';
import {
  DynamicDialogConfig,
  DynamicDialogRef,
} from 'primeng/dynamicdialog';
import { Subject, of, throwError } from 'rxjs';
import { GenerateTextService } from '../../services/generate-text.service';
import { LocalStorageService } from '../../services/local-storage.service';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { PromptType } from '../../types/enums/prompt-type';
import {
  DescribeImageComponent,
  DescribeImageComponentData,
} from './describe-image.component';

describe('DescribeImageComponent', () => {
  let component: DescribeImageComponent;
  let generateTextService: jasmine.SpyObj<GenerateTextService>;
  let localStorageService: jasmine.SpyObj<LocalStorageService>;
  let toastrService: jasmine.SpyObj<ToastrService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let sanitizer: jasmine.SpyObj<DomSanitizer>;
  let config: { data: DescribeImageComponentData };
  let createObjectUrlSpy: jasmine.Spy;
  let revokeObjectUrlSpy: jasmine.Spy;

  const image = new File(['pixels'], 'image.png', { type: 'image/png' });

  const createComponent = (
    data: DescribeImageComponentData = { image },
  ): DescribeImageComponent => {
    config.data = data;
    return TestBed.runInInjectionContext(() => new DescribeImageComponent());
  };

  beforeEach(() => {
    generateTextService = jasmine.createSpyObj<GenerateTextService>(
      'GenerateTextService',
      ['describeImage'],
    );
    localStorageService = jasmine.createSpyObj<LocalStorageService>(
      'LocalStorageService',
      ['getNestedStringForKey', 'setNestedStringForKey'],
    );
    toastrService = jasmine.createSpyObj<ToastrService>('ToastrService', [
      'error',
      'warning',
    ]);
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>('DynamicDialogRef', [
      'close',
    ]);
    sanitizer = jasmine.createSpyObj<DomSanitizer>('DomSanitizer', [
      'bypassSecurityTrustUrl',
    ]);
    config = { data: { image } };

    createObjectUrlSpy = spyOn(URL, 'createObjectURL').and.returnValue(
      'blob:describe-image',
    );
    revokeObjectUrlSpy = spyOn(URL, 'revokeObjectURL');
    sanitizer.bypassSecurityTrustUrl.and.returnValue(
      'safe:describe-image' as SafeUrl,
    );
    localStorageService.getNestedStringForKey.and.returnValue(null);
    generateTextService.describeImage.and.returnValue(of('A description'));

    TestBed.configureTestingModule({
      providers: [
        { provide: GenerateTextService, useValue: generateTextService },
        { provide: LocalStorageService, useValue: localStorageService },
        { provide: ToastrService, useValue: toastrService },
        { provide: DynamicDialogRef, useValue: dialogRef },
        { provide: DynamicDialogConfig, useValue: config },
        { provide: DomSanitizer, useValue: sanitizer },
      ],
    });

    component = createComponent();
  });

  it('creates and sanitizes a preview, then releases its object URL', () => {
    expect(createObjectUrlSpy).toHaveBeenCalledOnceWith(image);
    expect(sanitizer.bypassSecurityTrustUrl).toHaveBeenCalledOnceWith(
      'blob:describe-image',
    );
    expect(component.imagePreview).toBe('safe:describe-image' as SafeUrl);

    component.ngOnDestroy();

    expect(revokeObjectUrlSpy).toHaveBeenCalledOnceWith('blob:describe-image');
  });

  it('restores the recent prompt and instructions for the selected prompt type', () => {
    localStorageService.getNestedStringForKey.and.callFake((key) =>
      key === LocalStorageKey.RecentInstructions
        ? 'Use concrete details'
        : 'prompt-id',
    );

    component = createComponent({
      image,
      promptType: PromptType.DescribeCompendiumImage,
      compendiumId: 'compendium-id',
    });

    expect(component.formGroup.getRawValue()).toEqual({
      promptId: 'prompt-id',
      model: '',
      instructions: 'Use concrete details',
    });
    expect(component.selectedPromptType).toBe(
      PromptType.DescribeCompendiumImage,
    );
    expect(localStorageService.getNestedStringForKey).toHaveBeenCalledWith(
      LocalStorageKey.RecentPrompts,
      PromptType.DescribeCompendiumImage,
    );
  });

  it('uses the general image-description prompt type by default', () => {
    expect(component.selectedPromptType).toBe(PromptType.DescribeImage);
  });

  it('rejects an invalid form without calling the service', () => {
    component.describeImage();

    expect(toastrService.error).toHaveBeenCalledOnceWith(
      'Please fill out all required fields',
    );
    expect(generateTextService.describeImage).not.toHaveBeenCalled();
  });

  it('requires compendium context for compendium image descriptions', () => {
    component = createComponent({
      image,
      promptType: PromptType.DescribeCompendiumImage,
      compendiumId: '  ',
    });
    component.formGroup.patchValue({
      promptId: 'prompt-id',
      model: 'vision-model',
    });

    component.describeImage();

    expect(toastrService.error).toHaveBeenCalledWith(
      'Compendium context is required for this prompt type',
    );
    expect(generateTextService.describeImage).not.toHaveBeenCalled();
  });

  it('maps a general description request and updates its result', () => {
    component.formGroup.setValue({
      promptId: 'prompt-id',
      model: 'vision-model',
      instructions: 'Focus on lighting',
    });

    component.describeImage();

    expect(generateTextService.describeImage).toHaveBeenCalledOnceWith(image, {
      model: 'vision-model',
      promptId: 'prompt-id',
      instructions: 'Focus on lighting',
    });
    expect(component.description).toBe('A description');
    expect(component.isGenerating).toBeFalse();
  });

  it('adds compendium context to a compendium description request', () => {
    component = createComponent({
      image,
      promptType: PromptType.DescribeCompendiumImage,
      compendiumId: 'compendium-id',
    });
    component.formGroup.setValue({
      promptId: 'prompt-id',
      model: 'vision-model',
      instructions: null,
    });

    component.describeImage();

    expect(generateTextService.describeImage).toHaveBeenCalledWith(image, {
      model: 'vision-model',
      promptId: 'prompt-id',
      instructions: null,
      compendiumId: 'compendium-id',
    });
  });

  it('guards concurrent requests until the active request finishes', () => {
    const descriptions = new Subject<string>();
    generateTextService.describeImage.and.returnValue(descriptions);
    component.formGroup.patchValue({
      promptId: 'prompt-id',
      model: 'vision-model',
    });

    component.describeImage();
    component.describeImage();

    expect(generateTextService.describeImage).toHaveBeenCalledTimes(1);
    expect(component.isGenerating).toBeTrue();

    descriptions.complete();

    expect(component.isGenerating).toBeFalse();
  });

  it('reports request failures and resets its loading state', () => {
    generateTextService.describeImage.and.returnValue(
      throwError(() => new Error('request failed')),
    );
    component.formGroup.patchValue({
      promptId: 'prompt-id',
      model: 'vision-model',
    });

    component.describeImage();

    expect(component.isGenerating).toBeFalse();
    expect(component.description).toBeNull();
    expect(toastrService.error).toHaveBeenCalledOnceWith(
      'Failed to describe image',
    );
  });

  it('ignores blank descriptions when accepting', () => {
    component.description = '   ';

    component.accept();

    expect(dialogRef.close).not.toHaveBeenCalled();
    expect(localStorageService.setNestedStringForKey).not.toHaveBeenCalled();
  });

  it('trims accepted descriptions and persists recent choices', () => {
    component.description = '  A bright castle  ';
    component.formGroup.patchValue({
      promptId: 'prompt-id',
      instructions: 'Be concise',
    });

    component.accept();

    expect(localStorageService.setNestedStringForKey).toHaveBeenCalledWith(
      LocalStorageKey.RecentInstructions,
      PromptType.DescribeImage,
      'Be concise',
    );
    expect(localStorageService.setNestedStringForKey).toHaveBeenCalledWith(
      LocalStorageKey.RecentPrompts,
      PromptType.DescribeImage,
      'prompt-id',
    );
    expect(dialogRef.close).toHaveBeenCalledOnceWith('A bright castle');
  });

  it('tracks prompt availability and warns when none are available', () => {
    component.onPromptOptionsChanged(0);

    expect(component.promptCount).toBe(0);
    expect(toastrService.warning).toHaveBeenCalledOnceWith(
      'No prompts are available for image description',
    );

    component.onPromptOptionsChanged(3);

    expect(component.promptCount).toBe(3);
    expect(toastrService.warning).toHaveBeenCalledTimes(1);
  });
});
