import {
  HttpEvent,
  HttpResponse,
} from '@angular/common/http';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { TestBed } from '@angular/core/testing';
import { ToastrService } from 'ngx-toastr';
import {
  DynamicDialogConfig,
  DynamicDialogRef,
} from 'primeng/dynamicdialog';
import { Subject, of, throwError } from 'rxjs';
import { GenerateImageService } from '../../services/generate-image.service';
import { LocalStorageService } from '../../services/local-storage.service';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import {
  EditImageComponent,
  EditImageComponentData,
} from './edit-image.component';

describe('EditImageComponent', () => {
  let component: EditImageComponent;
  let generateImageService: jasmine.SpyObj<GenerateImageService>;
  let localStorageService: jasmine.SpyObj<LocalStorageService>;
  let toastrService: jasmine.SpyObj<ToastrService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let sanitizer: jasmine.SpyObj<DomSanitizer>;
  let config: { data: EditImageComponentData };
  let createObjectUrlSpy: jasmine.Spy;
  let revokeObjectUrlSpy: jasmine.Spy;

  const originalImage = new File(['original'], 'original.png', {
    type: 'image/png',
  });

  const createComponent = (
    data: EditImageComponentData = {
      image: originalImage,
      width: 640,
      height: 480,
    },
  ): EditImageComponent => {
    config.data = data;
    return TestBed.runInInjectionContext(() => new EditImageComponent());
  };

  beforeEach(() => {
    generateImageService = jasmine.createSpyObj<GenerateImageService>(
      'GenerateImageService',
      ['editImage'],
    );
    localStorageService = jasmine.createSpyObj<LocalStorageService>(
      'LocalStorageService',
      [
        'getNestedStringForKey',
        'getStringForKey',
        'setNestedStringForKey',
      ],
    );
    toastrService = jasmine.createSpyObj<ToastrService>('ToastrService', [
      'error',
    ]);
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>('DynamicDialogRef', [
      'close',
    ]);
    sanitizer = jasmine.createSpyObj<DomSanitizer>('DomSanitizer', [
      'bypassSecurityTrustUrl',
    ]);
    config = { data: {} };

    createObjectUrlSpy = spyOn(URL, 'createObjectURL').and.returnValues(
      'blob:original',
      'blob:generated-one',
      'blob:generated-two',
    );
    revokeObjectUrlSpy = spyOn(URL, 'revokeObjectURL');
    sanitizer.bypassSecurityTrustUrl.and.callFake(
      (url) => `safe:${url}` as SafeUrl,
    );
    localStorageService.getNestedStringForKey.and.returnValue(null);
    localStorageService.getStringForKey.and.returnValue(null);
    generateImageService.editImage.and.returnValue(
      of(new HttpResponse({ body: new Blob(['edited'], { type: 'image/png' }) })),
    );

    TestBed.configureTestingModule({
      providers: [
        { provide: GenerateImageService, useValue: generateImageService },
        { provide: LocalStorageService, useValue: localStorageService },
        { provide: ToastrService, useValue: toastrService },
        { provide: DynamicDialogRef, useValue: dialogRef },
        { provide: DynamicDialogConfig, useValue: config },
        { provide: DomSanitizer, useValue: sanitizer },
      ],
    });

    component = createComponent();
  });

  it('restores the contextual prompt and initializes the original preview', () => {
    localStorageService.getNestedStringForKey.and.returnValue('Remove the fog');
    localStorageService.getStringForKey.calls.reset();

    component = createComponent();

    expect(component.formGroup.controls.prompt.value).toBe('Remove the fog');
    expect(component.formGroup.dirty).toBeTrue();
    expect(component.originalImage).toBe(originalImage);
    expect(component.originalImagePreview).toBe(
      'safe:blob:generated-one' as SafeUrl,
    );
    expect(component.width).toBe(640);
    expect(component.height).toBe(480);
    expect(localStorageService.getNestedStringForKey).toHaveBeenCalledWith(
      LocalStorageKey.LastImagePromptByContext,
      'edit',
    );
    expect(localStorageService.getStringForKey).not.toHaveBeenCalled();
  });

  it('falls back to the legacy image prompt', () => {
    localStorageService.getStringForKey.and.returnValue('Legacy prompt');
    localStorageService.getStringForKey.calls.reset();

    component = createComponent();

    expect(component.formGroup.controls.prompt.value).toBe('Legacy prompt');
    expect(localStorageService.getStringForKey).toHaveBeenCalledOnceWith(
      LocalStorageKey.LastImagePrompt,
    );
  });

  it('rejects incomplete forms and missing source images', () => {
    component.editImage();
    expect(toastrService.error).toHaveBeenCalledWith(
      'Please fill out all fields',
    );

    component = createComponent({ width: 100, height: 100 });
    component.formGroup.setValue({
      prompt: 'Add stars',
      model: 'edit-model',
    });
    component.editImage();

    expect(toastrService.error).toHaveBeenCalledTimes(2);
    expect(generateImageService.editImage).not.toHaveBeenCalled();
  });

  it('persists choices and maps an editing request', () => {
    component.formGroup.setValue({
      prompt: 'Add stars',
      model: 'edit-model',
    });

    component.editImage();

    expect(localStorageService.setNestedStringForKey).toHaveBeenCalledWith(
      LocalStorageKey.LastImagePromptByContext,
      'edit',
      'Add stars',
    );
    expect(localStorageService.setNestedStringForKey).toHaveBeenCalledWith(
      LocalStorageKey.LastImageModelByContext,
      'edit',
      'edit-model',
    );
    expect(generateImageService.editImage).toHaveBeenCalledOnceWith(
      originalImage,
      {
        modelId: 'edit-model',
        prompt: 'Add stars',
        width: 640,
        height: 480,
      },
    );
  });

  it('uses only response events to create an edited preview', () => {
    const edited = new Blob(['edited'], { type: 'image/webp' });
    const events = new Subject<HttpEvent<Blob>>();
    generateImageService.editImage.and.returnValue(events);
    component.formGroup.setValue({
      prompt: 'Add stars',
      model: 'edit-model',
    });

    component.editImage();
    events.next({ type: 0 });

    expect(component.imageBlob).toBeNull();
    expect(component.isGenerating).toBeTrue();

    events.next(new HttpResponse({ body: edited }));
    events.complete();

    expect(component.imageBlob).toBe(edited);
    expect(component.imagePreview).toBe(
      'safe:blob:generated-one' as SafeUrl,
    );
    expect(component.isGenerating).toBeFalse();
    expect(component.lastGenerationDurationSeconds).not.toBeNull();
  });

  it('guards concurrent editing requests', () => {
    const events = new Subject<HttpEvent<Blob>>();
    generateImageService.editImage.and.returnValue(events);
    component.formGroup.setValue({
      prompt: 'Add stars',
      model: 'edit-model',
    });

    component.editImage();
    component.editImage();

    expect(generateImageService.editImage).toHaveBeenCalledTimes(1);

    events.complete();
  });

  it('reports failures and restores the idle state', () => {
    spyOn(console, 'error');
    generateImageService.editImage.and.returnValue(
      throwError(() => new Error('request failed')),
    );
    component.formGroup.setValue({
      prompt: 'Add stars',
      model: 'edit-model',
    });

    component.editImage();

    expect(component.isGenerating).toBeFalse();
    expect(toastrService.error).toHaveBeenCalledOnceWith(
      'Image editing failed',
    );
  });

  it('exposes generation labels for active and completed requests', () => {
    component.isGenerating = true;
    component.generationElapsedSeconds = 7;

    expect(component.editButtonLabel).toBe('Editing (7s)');
    expect(component.generationStatusLabel).toBeNull();

    component.isGenerating = false;
    component.lastGenerationDurationSeconds = 9;

    expect(component.editButtonLabel).toBe('Edit');
    expect(component.generationStatusLabel).toBe('Editing took 9s');
  });

  it('closes with the generated blob when accepted', () => {
    const edited = new Blob(['edited'], { type: 'image/png' });
    component.imageBlob = edited;

    component.accept();

    expect(dialogRef.close).toHaveBeenCalledOnceWith(edited);
  });

  it('releases replaced and remaining preview URLs', () => {
    const first = new Blob(['first'], { type: 'image/png' });
    const second = new Blob(['second'], { type: 'image/png' });
    const events = new Subject<HttpEvent<Blob>>();
    generateImageService.editImage.and.returnValue(events);
    component.formGroup.setValue({
      prompt: 'Add stars',
      model: 'edit-model',
    });

    component.editImage();
    events.next(new HttpResponse({ body: first }));
    events.next(new HttpResponse({ body: second }));
    component.ngOnDestroy();

    expect(revokeObjectUrlSpy).toHaveBeenCalledWith('blob:generated-one');
    expect(revokeObjectUrlSpy).toHaveBeenCalledWith('blob:original');
    expect(revokeObjectUrlSpy).toHaveBeenCalledWith('blob:generated-two');
  });
});
