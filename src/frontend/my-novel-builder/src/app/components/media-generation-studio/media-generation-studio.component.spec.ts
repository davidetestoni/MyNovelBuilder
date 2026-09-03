import {
  HttpEvent,
  HttpEventType,
  HttpResponse,
} from '@angular/common/http';
import {
  fakeAsync,
  flushMicrotasks,
  TestBed,
  tick,
} from '@angular/core/testing';
import { SimpleChange } from '@angular/core';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { ToastrService } from 'ngx-toastr';
import { Subject, of, throwError } from 'rxjs';
import { GenerateImageService } from '../../services/generate-image.service';
import { GenerateVideoService } from '../../services/generate-video.service';
import { LocalStorageService } from '../../services/local-storage.service';
import { MediaLibraryService } from '../../services/media-library.service';
import { MediaFileDto } from '../../types/dtos/media-library/media-file.dto';
import { MediaFolderDto } from '../../types/dtos/media-library/media-folder.dto';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { MediaGenerationStudioComponent } from './media-generation-studio.component';

describe('MediaGenerationStudioComponent', () => {
  const originalClipboardDescriptor = Object.getOwnPropertyDescriptor(
    navigator,
    'clipboard',
  );
  let component: MediaGenerationStudioComponent;
  let generateImageService: jasmine.SpyObj<GenerateImageService>;
  let generateVideoService: jasmine.SpyObj<GenerateVideoService>;
  let localStorageService: jasmine.SpyObj<LocalStorageService>;
  let mediaLibraryService: jasmine.SpyObj<MediaLibraryService>;
  let toastrService: jasmine.SpyObj<ToastrService>;
  let sanitizer: jasmine.SpyObj<DomSanitizer>;
  let createObjectUrlSpy: jasmine.Spy;
  let revokeObjectUrlSpy: jasmine.Spy;
  let objectUrlIndex: number;

  const imageBlob = new Blob(['image'], { type: 'image/png' });
  const videoBlob = new Blob(['video'], { type: 'video/mp4' });
  const sourceImage = new File(['source'], 'source.webp', {
    type: 'image/webp',
  });
  const folder = (id: string): MediaFolderDto => ({
    id,
    name: `Folder ${id}`,
    path: `/media/${id}`,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  });
  const uploadedMedia: MediaFileDto = {
    fileName: 'generated.png',
    lastModifiedAt: '2026-01-01T00:00:00Z',
    sizeBytes: 5,
  };

  const createComponent = (): MediaGenerationStudioComponent =>
    TestBed.runInInjectionContext(() => new MediaGenerationStudioComponent());

  const configureValidForm = (
    mode: 'image' | 'textToVideo' | 'imageToVideo' = 'image',
    batchSize = 1,
  ): void => {
    component.formGroup.setValue({
      prompt: 'A city beneath the stars',
      model: `${mode}-model`,
      mode,
      batchSize,
    });
  };

  const setFileInput = (file: File | null): HTMLInputElement => {
    const input = document.createElement('input');
    Object.defineProperty(input, 'files', {
      configurable: true,
      value: file === null ? [] : [file],
    });
    return input;
  };

  const clipboardEvent = (
    file: File | null,
    preventDefault = jasmine.createSpy('preventDefault'),
  ): ClipboardEvent =>
    ({
      clipboardData: {
        items: [
          {
            kind: 'file',
            type: file?.type ?? 'text/plain',
            getAsFile: () => file,
          },
        ],
      },
      preventDefault,
    }) as unknown as ClipboardEvent;

  beforeEach(() => {
    generateImageService = jasmine.createSpyObj<GenerateImageService>(
      'GenerateImageService',
      ['generateImage'],
    );
    generateVideoService = jasmine.createSpyObj<GenerateVideoService>(
      'GenerateVideoService',
      ['generateVideo', 'generateVideoFromImage'],
    );
    localStorageService = jasmine.createSpyObj<LocalStorageService>(
      'LocalStorageService',
      [
        'getNestedStringForKey',
        'getStringForKey',
        'setNestedStringForKey',
        'setStringForKey',
      ],
    );
    mediaLibraryService = jasmine.createSpyObj<MediaLibraryService>(
      'MediaLibraryService',
      ['uploadMedia'],
    );
    toastrService = jasmine.createSpyObj<ToastrService>('ToastrService', [
      'error',
      'success',
    ]);
    sanitizer = jasmine.createSpyObj<DomSanitizer>('DomSanitizer', [
      'bypassSecurityTrustUrl',
    ]);

    localStorageService.getNestedStringForKey.and.returnValue(null);
    localStorageService.getStringForKey.and.returnValue(null);
    generateImageService.generateImage.and.returnValue(
      of(new HttpResponse({ body: imageBlob })),
    );
    generateVideoService.generateVideo.and.returnValue(
      of(new HttpResponse({ body: videoBlob })),
    );
    generateVideoService.generateVideoFromImage.and.returnValue(
      of(new HttpResponse({ body: videoBlob })),
    );
    mediaLibraryService.uploadMedia.and.returnValue(of(uploadedMedia));

    objectUrlIndex = 0;
    createObjectUrlSpy = spyOn(URL, 'createObjectURL').and.callFake(
      () => `blob:studio-${++objectUrlIndex}`,
    );
    revokeObjectUrlSpy = spyOn(URL, 'revokeObjectURL');
    sanitizer.bypassSecurityTrustUrl.and.callFake(
      (url) => `safe:${url}` as SafeUrl,
    );

    TestBed.configureTestingModule({
      providers: [
        { provide: GenerateImageService, useValue: generateImageService },
        { provide: GenerateVideoService, useValue: generateVideoService },
        { provide: LocalStorageService, useValue: localStorageService },
        { provide: MediaLibraryService, useValue: mediaLibraryService },
        { provide: ToastrService, useValue: toastrService },
        { provide: DomSanitizer, useValue: sanitizer },
      ],
    });

    component = createComponent();
    component.folder = folder('one');
  });

  afterEach(() => {
    if (originalClipboardDescriptor === undefined) {
      delete (navigator as { clipboard?: Clipboard }).clipboard;
      return;
    }

    Object.defineProperty(
      navigator,
      'clipboard',
      originalClipboardDescriptor,
    );
  });

  it('starts with image-generation defaults and supported batch sizes', () => {
    expect(component.formGroup.getRawValue()).toEqual({
      prompt: '',
      model: '',
      mode: 'image',
      batchSize: 4,
    });
    expect(component.batchSizeOptions.map(({ value }) => value)).toEqual([
      1, 2, 3, 4, 6, 8,
    ]);
    expect(component.generationModeOptions.map(({ value }) => value)).toEqual([
      'image',
      'textToVideo',
      'imageToVideo',
    ]);
  });

  it('restores a contextual image prompt before the legacy prompt', () => {
    localStorageService.getNestedStringForKey.and.returnValue(
      'Contextual image prompt',
    );
    localStorageService.getStringForKey.and.returnValue('Legacy prompt');
    localStorageService.getStringForKey.calls.reset();

    component = createComponent();

    expect(component.formGroup.controls.prompt.value).toBe(
      'Contextual image prompt',
    );
    expect(component.formGroup.dirty).toBeTrue();
    expect(localStorageService.getNestedStringForKey).toHaveBeenCalledWith(
      LocalStorageKey.LastImagePromptByContext,
      'generate',
    );
    expect(localStorageService.getStringForKey).not.toHaveBeenCalled();
  });

  it('falls back to a non-empty legacy image prompt', () => {
    localStorageService.getStringForKey.and.returnValue('Legacy image prompt');
    localStorageService.getStringForKey.calls.reset();

    component = createComponent();

    expect(component.formGroup.controls.prompt.value).toBe(
      'Legacy image prompt',
    );
    expect(localStorageService.getStringForKey).toHaveBeenCalledOnceWith(
      LocalStorageKey.LastImagePrompt,
    );
  });

  it('ignores empty stored prompts', () => {
    localStorageService.getNestedStringForKey.and.returnValue('   ');

    component = createComponent();

    expect(component.formGroup.controls.prompt.value).toBe('');
    expect(component.formGroup.pristine).toBeTrue();
  });

  it('derives labels, placeholders, capabilities, and batch visibility by mode', () => {
    expect(component.selectedCapability).toBe('imageGeneration');
    expect(component.outputLabel).toBe('image');
    expect(component.generateButtonLabel).toBe('Generate');
    expect(component.promptPlaceholder).toContain('image');
    expect(component.shouldShowBatchSize).toBeTrue();

    component.formGroup.controls.mode.setValue('textToVideo');
    expect(component.selectedCapability).toBe('textToVideo');
    expect(component.outputLabel).toBe('video');
    expect(component.generateButtonLabel).toBe('Generate Video');
    expect(component.promptPlaceholder).toContain('video');
    expect(component.shouldShowBatchSize).toBeFalse();

    component.formGroup.controls.mode.setValue('imageToVideo');
    expect(component.selectedCapability).toBe('imageToVideo');
    expect(component.generateButtonLabel).toBe('Generate from Image');
    expect(component.promptPlaceholder).toContain('uploaded image');
  });

  it('restores video prompts for each video-generation context', () => {
    localStorageService.getNestedStringForKey.and.callFake((_key, context) =>
      context === 'generate-video'
        ? 'Animate the clouds'
        : context === 'generate-image-to-video'
          ? 'Move the camera'
          : null,
    );

    component.formGroup.controls.mode.setValue('textToVideo');
    expect(component.formGroup.controls.prompt.value).toBe(
      'Animate the clouds',
    );
    expect(localStorageService.getNestedStringForKey).toHaveBeenCalledWith(
      LocalStorageKey.LastVideoPromptByContext,
      'generate-video',
    );

    component.formGroup.controls.mode.setValue('imageToVideo');
    expect(component.formGroup.controls.prompt.value).toBe('Move the camera');
    expect(localStorageService.getNestedStringForKey).toHaveBeenCalledWith(
      LocalStorageKey.LastVideoPromptByContext,
      'generate-image-to-video',
    );
  });

  it('tracks the current folder and clears previews only after its id changes', () => {
    configureValidForm();
    component.generateMedia();
    const originalAsset = component.generatedAssets[0];

    component.ngOnChanges({
      folder: new SimpleChange(null, component.folder, true),
    });
    expect(component.generatedAssets).toEqual([originalAsset]);

    component.folder = folder('two');
    component.ngOnChanges({
      folder: new SimpleChange(folder('one'), component.folder, false),
    });

    expect(component.generatedAssets).toEqual([]);
    expect(revokeObjectUrlSpy).toHaveBeenCalledOnceWith(originalAsset.objectUrl);
  });

  it('requires a selected folder before generation', () => {
    component.folder = null;
    configureValidForm();

    component.generateMedia();

    expect(toastrService.error).toHaveBeenCalledOnceWith(
      'Select a media folder first.',
    );
    expect(generateImageService.generateImage).not.toHaveBeenCalled();
  });

  it('rejects invalid and whitespace-only generation fields', () => {
    component.generateMedia();

    component.formGroup.setValue({
      prompt: '   ',
      model: 'image-model',
      mode: 'image',
      batchSize: 1,
    });
    component.generateMedia();

    component.formGroup.setValue({
      prompt: 'A valid prompt',
      model: '   ',
      mode: 'image',
      batchSize: 1,
    });
    component.generateMedia();

    expect(toastrService.error).toHaveBeenCalledTimes(3);
    expect(generateImageService.generateImage).not.toHaveBeenCalled();
  });

  it('requires a source image for image-to-video generation', () => {
    configureValidForm('imageToVideo');

    component.generateMedia();

    expect(toastrService.error).toHaveBeenCalledOnceWith(
      'Upload a source image first.',
    );
    expect(
      generateVideoService.generateVideoFromImage,
    ).not.toHaveBeenCalled();
  });

  it('generates an image batch and persists the image choices', () => {
    configureValidForm('image', 3);

    component.generateMedia();

    expect(generateImageService.generateImage).toHaveBeenCalledTimes(3);
    expect(generateImageService.generateImage).toHaveBeenCalledWith({
      modelId: 'image-model',
      prompt: 'A city beneath the stars',
      width: 832,
      height: 1248,
    });
    expect(component.generatedAssets.length).toBe(3);
    expect(component.generatedAssets.map(({ previewUrl }) => previewUrl)).toEqual([
      'safe:blob:studio-1' as SafeUrl,
      'safe:blob:studio-2' as SafeUrl,
      'safe:blob:studio-3' as SafeUrl,
    ]);
    expect(component.isGenerating).toBeFalse();
    expect(localStorageService.setNestedStringForKey).toHaveBeenCalledWith(
      LocalStorageKey.LastImagePromptByContext,
      'generate',
      'A city beneath the stars',
    );
    expect(localStorageService.setStringForKey).toHaveBeenCalledWith(
      LocalStorageKey.LastImageModel,
      'image-model',
    );
  });

  it('routes a single text-to-video request and persists video choices', () => {
    configureValidForm('textToVideo', 8);

    component.generateMedia();

    expect(generateVideoService.generateVideo).toHaveBeenCalledOnceWith({
      modelId: 'textToVideo-model',
      prompt: 'A city beneath the stars',
      width: 832,
      height: 1248,
    });
    expect(component.generatedAssets.length).toBe(1);
    expect(component.isVideoAsset(component.generatedAssets[0])).toBeTrue();
    expect(localStorageService.setNestedStringForKey).toHaveBeenCalledWith(
      LocalStorageKey.LastVideoModelByContext,
      'generate-video',
      'textToVideo-model',
    );
    expect(localStorageService.setStringForKey).toHaveBeenCalledWith(
      LocalStorageKey.LastVideoPrompt,
      'A city beneath the stars',
    );
  });

  it('routes image-to-video generation with the selected source image', () => {
    configureValidForm('imageToVideo');
    component.sourceImageFile = sourceImage;

    component.generateMedia();

    expect(
      generateVideoService.generateVideoFromImage,
    ).toHaveBeenCalledOnceWith(sourceImage, {
      modelId: 'imageToVideo-model',
      prompt: 'A city beneath the stars',
      width: 832,
      height: 1248,
    });
    expect(localStorageService.setNestedStringForKey).toHaveBeenCalledWith(
      LocalStorageKey.LastVideoPromptByContext,
      'generate-image-to-video',
      'A city beneath the stars',
    );
  });

  it('waits for every response in a batch and ignores progress events', () => {
    const first = new Subject<HttpEvent<Blob>>();
    const second = new Subject<HttpEvent<Blob>>();
    generateImageService.generateImage.and.returnValues(first, second);
    configureValidForm('image', 2);

    component.generateMedia();
    first.next({ type: HttpEventType.Sent });
    first.next(new HttpResponse({ body: imageBlob }));
    first.complete();
    expect(component.generatedAssets).toEqual([]);
    expect(component.isGenerating).toBeTrue();

    second.next(new HttpResponse({ body: imageBlob }));
    second.complete();

    expect(component.generatedAssets.length).toBe(2);
    expect(component.isGenerating).toBeFalse();
  });

  it('guards concurrent generation calls', () => {
    const events = new Subject<HttpEvent<Blob>>();
    generateImageService.generateImage.and.returnValue(events);
    configureValidForm();

    component.generateMedia();
    component.generateMedia();

    expect(generateImageService.generateImage).toHaveBeenCalledTimes(1);
    expect(component.isGenerating).toBeTrue();

    events.complete();
  });

  it('reports generation failures and returns to the idle state', () => {
    const error = new Error('request failed');
    const consoleError = spyOn(console, 'error');
    generateImageService.generateImage.and.returnValue(
      throwError(() => error),
    );
    configureValidForm();

    component.generateMedia();

    expect(consoleError).toHaveBeenCalledOnceWith(
      'Media generation failed',
      error,
    );
    expect(toastrService.error).toHaveBeenCalledOnceWith(
      'Media generation failed.',
    );
    expect(component.isGenerating).toBeFalse();
    expect(component.generatedAssets).toEqual([]);
  });

  it('tracks elapsed generation time and the final status', fakeAsync(() => {
    const events = new Subject<HttpEvent<Blob>>();
    generateImageService.generateImage.and.returnValue(events);
    configureValidForm();

    component.generateMedia();
    tick(2100);

    expect(component.generationElapsedSeconds).toBe(2);
    expect(component.generateButtonLabel).toBe('Generating image (2s)');
    expect(component.generationStatusLabel).toBeNull();

    events.next(new HttpResponse({ body: imageBlob }));
    events.complete();

    expect(component.lastGenerationDurationSeconds).toBe(2);
    expect(component.generationStatusLabel).toBe(
      'Image generation took 2s',
    );
  }));

  it('cancels an in-flight batch when the generation mode changes', () => {
    const events = new Subject<HttpEvent<Blob>>();
    generateImageService.generateImage.and.returnValue(events);
    configureValidForm();
    component.generateMedia();

    component.formGroup.controls.mode.setValue('textToVideo');
    events.next(new HttpResponse({ body: imageBlob }));
    events.complete();

    expect(component.isGenerating).toBeFalse();
    expect(component.lastGenerationDurationSeconds).toBeNull();
    expect(component.generatedAssets).toEqual([]);
    expect(events.observers.length).toBe(0);
  });

  it('cancels an in-flight batch when the selected folder changes', () => {
    component.ngOnChanges({
      folder: new SimpleChange(null, component.folder, true),
    });
    const events = new Subject<HttpEvent<Blob>>();
    generateImageService.generateImage.and.returnValue(events);
    configureValidForm();
    component.generateMedia();

    component.folder = folder('two');
    component.ngOnChanges({
      folder: new SimpleChange(folder('one'), component.folder, false),
    });

    expect(component.isGenerating).toBeFalse();
    expect(component.lastGenerationDurationSeconds).toBeNull();
    expect(events.observers.length).toBe(0);
  });

  it('ignores source-selection events without a file input or selected file', () => {
    component.onSourceImageSelected({ target: document.body } as unknown as Event);
    component.onSourceImageSelected({
      target: setFileInput(null),
    } as unknown as Event);

    expect(component.sourceImageFile).toBeNull();
    expect(createObjectUrlSpy).not.toHaveBeenCalled();
  });

  it('selects and replaces source images while releasing previews', () => {
    const firstInput = setFileInput(sourceImage);
    component.onSourceImageSelected({
      target: firstInput,
    } as unknown as Event);

    const replacement = new File(['replacement'], 'replacement.png', {
      type: 'image/png',
    });
    const replacementInput = setFileInput(replacement);
    component.onSourceImageSelected({
      target: replacementInput,
    } as unknown as Event);

    expect(component.sourceImageFile).toBe(replacement);
    expect(component.sourceImagePreview).toBe(
      'safe:blob:studio-2' as SafeUrl,
    );
    expect(firstInput.value).toBe('');
    expect(replacementInput.value).toBe('');
    expect(revokeObjectUrlSpy).toHaveBeenCalledOnceWith('blob:studio-1');
  });

  it('clears source images and any generated assets together', () => {
    configureValidForm();
    component.generateMedia();
    const generatedUrl = component.generatedAssets[0].objectUrl;
    component.onSourceImageSelected({
      target: setFileInput(sourceImage),
    } as unknown as Event);
    const sourceUrl = 'blob:studio-2';

    component.clearSelectedSourceImage();

    expect(component.generatedAssets).toEqual([]);
    expect(component.sourceImageFile).toBeNull();
    expect(component.sourceImagePreview).toBeNull();
    expect(revokeObjectUrlSpy).toHaveBeenCalledWith(generatedUrl);
    expect(revokeObjectUrlSpy).toHaveBeenCalledWith(sourceUrl);
  });

  it('accepts pasted image data only in image-to-video mode', () => {
    const preventDefault = jasmine.createSpy('preventDefault');
    const event = clipboardEvent(sourceImage, preventDefault);

    component.onPaste(event);
    expect(preventDefault).not.toHaveBeenCalled();

    component.formGroup.controls.mode.setValue('imageToVideo');
    component.onPaste(event);

    expect(preventDefault).toHaveBeenCalledTimes(1);
    expect(component.sourceImageFile).toBe(sourceImage);
  });

  it('ignores paste events that do not contain an image', () => {
    component.formGroup.controls.mode.setValue('imageToVideo');
    const preventDefault = jasmine.createSpy('preventDefault');

    component.onPaste(clipboardEvent(null, preventDefault));

    expect(preventDefault).not.toHaveBeenCalled();
    expect(component.sourceImageFile).toBeNull();
  });

  it('reads a source image through the async clipboard API', fakeAsync(() => {
    const getType = jasmine
      .createSpy('getType')
      .and.returnValue(Promise.resolve(imageBlob));
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: {
        read: jasmine.createSpy('read').and.returnValue(
          Promise.resolve([{ types: ['image/png'], getType }]),
        ),
      },
    });
    component.formGroup.controls.mode.setValue('imageToVideo');

    component.pasteSourceImageFromClipboard();
    expect(component.isPastingSourceImage).toBeTrue();
    flushMicrotasks();

    expect(component.isPastingSourceImage).toBeFalse();
    expect(component.sourceImageFile?.name).toBe('source-image.png');
    expect(component.sourceImagePreview).toBe(
      'safe:blob:studio-1' as SafeUrl,
    );
  }));

  it('reports async clipboard failures and resets the busy state', fakeAsync(() => {
    const error = new Error('Clipboard permission denied');
    spyOn(console, 'error');
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: {
        read: jasmine.createSpy('read').and.returnValue(Promise.reject(error)),
      },
    });
    component.formGroup.controls.mode.setValue('imageToVideo');

    component.pasteSourceImageFromClipboard();
    component.pasteSourceImageFromClipboard();
    flushMicrotasks();

    expect(toastrService.error).toHaveBeenCalledOnceWith(
      'Clipboard permission denied',
    );
    expect(component.isPastingSourceImage).toBeFalse();
  }));

  it('does not invoke async clipboard access outside image-to-video mode', async () => {
    await component.pasteSourceImageFromClipboard();

    expect(component.isPastingSourceImage).toBeFalse();
    expect(toastrService.error).not.toHaveBeenCalled();
  });

  it('recognizes video assets by MIME type', () => {
    configureValidForm('textToVideo');
    component.generateMedia();

    expect(component.isVideoAsset(component.generatedAssets[0])).toBeTrue();

    component.formGroup.controls.mode.setValue('image');
    component.generateMedia();
    expect(component.isVideoAsset(component.generatedAssets[0])).toBeFalse();
  });

  it('ignores save requests without a folder or for busy and saved assets', () => {
    configureValidForm();
    component.generateMedia();
    const asset = component.generatedAssets[0];

    component.folder = null;
    component.saveGeneratedImage(asset, 0);
    component.folder = folder('one');
    asset.isSaving = true;
    component.saveGeneratedImage(asset, 0);
    asset.isSaving = false;
    asset.isSaved = true;
    component.saveGeneratedImage(asset, 0);

    expect(mediaLibraryService.uploadMedia).not.toHaveBeenCalled();
  });

  it('uploads generated images, marks them saved, and emits a refresh event', () => {
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date(2026, 6, 29, 12, 34, 56));
    try {
      configureValidForm();
      component.generateMedia();
      const asset = component.generatedAssets[0];
      const mediaSaved = jasmine.createSpy('mediaSaved');
      component.mediaSaved.subscribe(mediaSaved);

      component.saveGeneratedImage(asset, 0);

      const [folderId, fileName, file] =
        mediaLibraryService.uploadMedia.calls.mostRecent().args;
      expect(folderId).toBe('one');
      expect(fileName).toBe('generated-image-20260729-123456-1.png');
      expect(file.name).toBe(fileName);
      expect(file.type).toBe('image/png');
      expect(asset.isSaving).toBeFalse();
      expect(asset.isSaved).toBeTrue();
      expect(asset.savedFileName).toBe(fileName);
      expect(toastrService.success).toHaveBeenCalledOnceWith(
        'Media uploaded.',
      );
      expect(mediaSaved).toHaveBeenCalledTimes(1);
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('uses video naming when saving a generated video', () => {
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date(2026, 6, 29, 1, 2, 3));
    try {
      configureValidForm('textToVideo');
      component.generateMedia();

      component.saveGeneratedImage(component.generatedAssets[0], 1);

      const [, fileName, file] =
        mediaLibraryService.uploadMedia.calls.mostRecent().args;
      expect(fileName).toBe('generated-video-20260729-010203-2.mp4');
      expect(file.type).toBe('video/mp4');
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('reports failed uploads and makes the asset retryable', () => {
    mediaLibraryService.uploadMedia.and.returnValue(
      throwError(() => new Error('upload failed')),
    );
    configureValidForm();
    component.generateMedia();
    const asset = component.generatedAssets[0];

    component.saveGeneratedImage(asset, 0);

    expect(asset.isSaving).toBeFalse();
    expect(asset.isSaved).toBeFalse();
    expect(toastrService.error).toHaveBeenCalledOnceWith(
      'Media upload failed.',
    );
  });

  it('releases generated and source previews when destroyed', () => {
    configureValidForm('imageToVideo');
    component.onSourceImageSelected({
      target: setFileInput(sourceImage),
    } as unknown as Event);
    const sourceUrl = 'blob:studio-1';
    component.generateMedia();
    const generatedUrl = component.generatedAssets[0].objectUrl;

    component.ngOnDestroy();

    expect(component.isGenerating).toBeFalse();
    expect(component.generatedAssets).toEqual([]);
    expect(component.sourceImageFile).toBeNull();
    expect(revokeObjectUrlSpy).toHaveBeenCalledWith(generatedUrl);
    expect(revokeObjectUrlSpy).toHaveBeenCalledWith(sourceUrl);
  });

  it('unsubscribes an active generation request when destroyed', () => {
    const events = new Subject<HttpEvent<Blob>>();
    generateImageService.generateImage.and.returnValue(events);
    configureValidForm();
    component.generateMedia();

    component.ngOnDestroy();
    events.next(new HttpResponse({ body: imageBlob }));

    expect(events.observers.length).toBe(0);
    expect(component.isGenerating).toBeFalse();
    expect(component.generatedAssets).toEqual([]);
  });
});
