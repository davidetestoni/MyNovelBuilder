import {
  HttpEvent,
  HttpEventType,
  HttpResponse,
} from '@angular/common/http';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { TestBed } from '@angular/core/testing';
import { ToastrService } from 'ngx-toastr';
import {
  DialogService,
  DynamicDialogConfig,
  DynamicDialogRef,
} from 'primeng/dynamicdialog';
import { Subject, of, throwError } from 'rxjs';
import { GenerateImageService } from '../../services/generate-image.service';
import { GenerateVideoService } from '../../services/generate-video.service';
import { LocalStorageService } from '../../services/local-storage.service';
import { PromptService } from '../../services/prompt.service';
import { GenerateTextRequestDto } from '../../types/dtos/generate/generate-text-request.dto';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { PromptType } from '../../types/enums/prompt-type';
import { GenerateTextComponent } from '../generate-text/generate-text.component';
import { GenerateTextResultComponent } from '../generate-text-result/generate-text-result.component';
import {
  GenerateMediaComponent,
  GenerateMediaComponentData,
} from './generate-media.component';

describe('GenerateMediaComponent', () => {
  let component: GenerateMediaComponent;
  let generateImageService: jasmine.SpyObj<GenerateImageService>;
  let generateVideoService: jasmine.SpyObj<GenerateVideoService>;
  let localStorageService: jasmine.SpyObj<LocalStorageService>;
  let promptService: jasmine.SpyObj<PromptService>;
  let toastrService: jasmine.SpyObj<ToastrService>;
  let dialogService: jasmine.SpyObj<DialogService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let sanitizer: jasmine.SpyObj<DomSanitizer>;
  let config: { data: GenerateMediaComponentData };
  let revokeObjectUrlSpy: jasmine.Spy;

  const generatedImage = new Blob(['image'], { type: 'image/png' });
  const generatedVideo = new Blob(['video'], { type: 'video/mp4' });
  const sourceImage = new File(['source'], 'source.png', {
    type: 'image/png',
  });

  const prompt = (id: string, type: PromptType): PromptDto => ({
    id,
    name: id,
    type,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    messages: [],
  });

  const createComponent = (
    data: GenerateMediaComponentData = {},
    initialize = false,
  ): GenerateMediaComponent => {
    config.data = data;
    const created = TestBed.runInInjectionContext(
      () => new GenerateMediaComponent(),
    );
    if (initialize) {
      created.ngOnInit();
    }
    return created;
  };

  const dialogWithClose = <T>(
    closeEvents: Subject<T>,
  ): DynamicDialogRef =>
    ({
      close: jasmine.createSpy('close'),
      onClose: closeEvents.asObservable(),
    }) as unknown as DynamicDialogRef;

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
    promptService = jasmine.createSpyObj<PromptService>('PromptService', [
      'getPrompts',
    ]);
    toastrService = jasmine.createSpyObj<ToastrService>('ToastrService', [
      'error',
      'warning',
    ]);
    dialogService = jasmine.createSpyObj<DialogService>('DialogService', [
      'open',
    ]);
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>('DynamicDialogRef', [
      'close',
    ]);
    sanitizer = jasmine.createSpyObj<DomSanitizer>('DomSanitizer', [
      'bypassSecurityTrustUrl',
    ]);
    config = { data: {} };

    spyOn(URL, 'createObjectURL').and.returnValues(
      'blob:generated',
      'blob:source',
      'blob:replacement',
    );
    revokeObjectUrlSpy = spyOn(URL, 'revokeObjectURL');
    sanitizer.bypassSecurityTrustUrl.and.callFake(
      (url) => `safe:${url}` as SafeUrl,
    );
    localStorageService.getNestedStringForKey.and.returnValue(null);
    localStorageService.getStringForKey.and.returnValue(null);
    promptService.getPrompts.and.returnValue(of([]));
    generateImageService.generateImage.and.returnValue(
      of(new HttpResponse({ body: generatedImage })),
    );
    generateVideoService.generateVideo.and.returnValue(
      of(new HttpResponse({ body: generatedVideo })),
    );
    generateVideoService.generateVideoFromImage.and.returnValue(
      of(new HttpResponse({ body: generatedVideo })),
    );

    TestBed.configureTestingModule({
      providers: [
        { provide: GenerateImageService, useValue: generateImageService },
        { provide: GenerateVideoService, useValue: generateVideoService },
        { provide: LocalStorageService, useValue: localStorageService },
        { provide: PromptService, useValue: promptService },
        { provide: ToastrService, useValue: toastrService },
        { provide: DialogService, useValue: dialogService },
        { provide: DynamicDialogRef, useValue: dialogRef },
        { provide: DynamicDialogConfig, useValue: config },
        { provide: DomSanitizer, useValue: sanitizer },
      ],
    });

    component = createComponent();
  });

  it('restores contextual image prompts before legacy prompts', () => {
    localStorageService.getNestedStringForKey.and.returnValue(
      'Contextual prompt',
    );
    localStorageService.getStringForKey.and.returnValue('Legacy prompt');
    localStorageService.getStringForKey.calls.reset();

    component = createComponent();

    expect(component.formGroup.controls.prompt.value).toBe(
      'Contextual prompt',
    );
    expect(component.formGroup.dirty).toBeTrue();
    expect(localStorageService.getNestedStringForKey).toHaveBeenCalledWith(
      LocalStorageKey.LastImagePromptByContext,
      'generate',
    );
    expect(localStorageService.getStringForKey).not.toHaveBeenCalled();
  });

  it('filters prompt-generation prompts during initialization', () => {
    promptService.getPrompts.and.returnValue(
      of([
        prompt(
          'image-prompt',
          PromptType.CreateCompendiumRecordImageGenerationPrompt,
        ),
        prompt('other-prompt', PromptType.GenerateText),
      ]),
    );

    component = createComponent(
      {
        enablePromptGeneration: true,
        compendiumId: 'compendium-id',
        compendiumRecordId: 'record-id',
      },
      true,
    );

    expect(component.promptGenerationPrompts.map(({ id }) => id)).toEqual([
      'image-prompt',
    ]);
    expect(component.canGeneratePrompt()).toBeTrue();
    expect(component.isLoadingPromptGenerationPrompts).toBeFalse();
  });

  it('warns when prompt generation has no matching prompts', () => {
    promptService.getPrompts.and.returnValue(
      of([prompt('other-prompt', PromptType.GenerateText)]),
    );

    component = createComponent(
      {
        enablePromptGeneration: true,
        compendiumId: 'compendium-id',
        compendiumRecordId: 'record-id',
      },
      true,
    );

    expect(toastrService.warning).toHaveBeenCalledOnceWith(
      'No prompts are available for image prompt generation',
    );
    expect(component.canGeneratePrompt()).toBeFalse();
  });

  it('clears prompt loading and reports failures', () => {
    promptService.getPrompts.and.returnValue(
      throwError(() => new Error('request failed')),
    );

    component = createComponent(
      {
        enablePromptGeneration: true,
        compendiumId: 'compendium-id',
        compendiumRecordId: 'record-id',
      },
      true,
    );

    expect(component.isLoadingPromptGenerationPrompts).toBeFalse();
    expect(toastrService.error).toHaveBeenCalledOnceWith(
      'Failed to load prompt-generation prompts',
    );
  });

  it('derives labels, placeholders, and model capabilities from the mode', () => {
    expect(component.selectedCapability).toBe('imageGeneration');
    expect(component.outputLabel).toBe('image');
    expect(component.generateButtonLabel).toBe('Generate Image');

    component.formGroup.controls.mode.setValue('textToVideo');
    expect(component.selectedCapability).toBe('textToVideo');
    expect(component.outputLabel).toBe('video');
    expect(component.generateButtonLabel).toBe('Generate Video');
    expect(component.promptPlaceholder).toContain('video');

    component.formGroup.controls.mode.setValue('imageToVideo');
    expect(component.selectedCapability).toBe('imageToVideo');
    expect(component.generateButtonLabel).toBe('Generate from Image');
    expect(component.promptPlaceholder).toContain('uploaded image');
  });

  it('rejects invalid and whitespace-only generation requests', () => {
    component.generateMedia();

    component.formGroup.setValue({
      prompt: '   ',
      model: 'model-id',
      mode: 'image',
    });
    component.generateMedia();

    expect(toastrService.error).toHaveBeenCalledTimes(2);
    expect(generateImageService.generateImage).not.toHaveBeenCalled();
  });

  it('requires a source image for image-to-video generation', () => {
    component.formGroup.setValue({
      prompt: 'Camera slowly pans',
      model: 'video-model',
      mode: 'imageToVideo',
    });

    component.generateMedia();

    expect(toastrService.error).toHaveBeenCalledOnceWith(
      'Please upload a source image first.',
    );
    expect(generateVideoService.generateVideoFromImage).not.toHaveBeenCalled();
  });

  it('generates an image with configured dimensions and persists its choices', () => {
    component = createComponent({ width: 1024, height: 768 });
    component.formGroup.setValue({
      prompt: 'A moonlit tower',
      model: 'image-model',
      mode: 'image',
    });

    component.generateMedia();

    expect(generateImageService.generateImage).toHaveBeenCalledOnceWith({
      modelId: 'image-model',
      prompt: 'A moonlit tower',
      width: 1024,
      height: 768,
    });
    expect(component.generatedBlob).toBe(generatedImage);
    expect(component.generatedPreview).toBe('safe:blob:generated' as SafeUrl);
    expect(component.isGenerating).toBeFalse();
    expect(localStorageService.setNestedStringForKey).toHaveBeenCalledWith(
      LocalStorageKey.LastImagePromptByContext,
      'generate',
      'A moonlit tower',
    );
    expect(localStorageService.setStringForKey).toHaveBeenCalledWith(
      LocalStorageKey.LastImageModel,
      'image-model',
    );
  });

  it('routes text-to-video requests and recognizes video responses', () => {
    component.formGroup.setValue({
      prompt: 'Clouds gather',
      model: 'video-model',
      mode: 'textToVideo',
    });

    component.generateMedia();

    expect(generateVideoService.generateVideo).toHaveBeenCalledOnceWith({
      modelId: 'video-model',
      prompt: 'Clouds gather',
      width: 832,
      height: 1248,
    });
    expect(generateImageService.generateImage).not.toHaveBeenCalled();
    expect(component.generatedBlob).toBe(generatedVideo);
    expect(component.hasGeneratedVideo).toBeTrue();
    expect(localStorageService.setNestedStringForKey).toHaveBeenCalledWith(
      LocalStorageKey.LastVideoModelByContext,
      'generate-video',
      'video-model',
    );
  });

  it('routes image-to-video requests with the selected source image', () => {
    component.formGroup.setValue({
      prompt: 'The leaves move',
      model: 'video-model',
      mode: 'imageToVideo',
    });
    component.sourceImageFile = sourceImage;

    component.generateMedia();

    expect(
      generateVideoService.generateVideoFromImage,
    ).toHaveBeenCalledOnceWith(sourceImage, {
      modelId: 'video-model',
      prompt: 'The leaves move',
      width: 832,
      height: 1248,
    });
  });

  it('guards concurrent generation requests', () => {
    const events = new Subject<HttpEvent<Blob>>();
    generateImageService.generateImage.and.returnValue(events);
    component.formGroup.setValue({
      prompt: 'A moonlit tower',
      model: 'image-model',
      mode: 'image',
    });

    component.generateMedia();
    component.generateMedia();

    expect(generateImageService.generateImage).toHaveBeenCalledTimes(1);
    expect(component.isGenerating).toBeTrue();

    events.complete();
  });

  it('reports generation errors and returns to the idle state', () => {
    spyOn(console, 'error');
    generateImageService.generateImage.and.returnValue(
      throwError(() => new Error('request failed')),
    );
    component.formGroup.setValue({
      prompt: 'A moonlit tower',
      model: 'image-model',
      mode: 'image',
    });

    component.generateMedia();

    expect(component.isGenerating).toBeFalse();
    expect(toastrService.error).toHaveBeenCalledOnceWith(
      'Media generation failed.',
    );
  });

  it('ignores progress events and accepts the generated result', () => {
    const events = new Subject<HttpEvent<Blob>>();
    generateImageService.generateImage.and.returnValue(events);
    component.formGroup.setValue({
      prompt: 'A moonlit tower',
      model: 'image-model',
      mode: 'image',
    });

    component.generateMedia();
    events.next({ type: HttpEventType.Sent });
    expect(component.generatedBlob).toBeNull();

    events.next(new HttpResponse({ body: generatedImage }));
    events.complete();
    component.accept();

    expect(dialogRef.close).toHaveBeenCalledOnceWith(generatedImage);
  });

  it('selects and clears source images while releasing previews', () => {
    component = createComponent({}, true);
    component.formGroup.controls.mode.setValue('imageToVideo');
    const input = document.createElement('input');
    Object.defineProperty(input, 'files', { value: [sourceImage] });

    component.onSourceImageSelected({ target: input } as unknown as Event);

    expect(component.sourceImageFile).toBe(sourceImage);
    expect(component.sourceImagePreview).toBe('safe:blob:generated' as SafeUrl);
    expect(input.value).toBe('');

    component.clearSelectedSourceImage();

    expect(component.sourceImageFile).toBeNull();
    expect(component.sourceImagePreview).toBeNull();
    expect(revokeObjectUrlSpy).toHaveBeenCalledOnceWith('blob:generated');
  });

  it('handles pasted source images only in image-to-video mode', () => {
    const preventDefault = jasmine.createSpy('preventDefault');
    const clipboardEvent = {
      clipboardData: {
        items: [
          {
            kind: 'file',
            type: 'image/png',
            getAsFile: () => sourceImage,
          },
        ],
      },
      preventDefault,
    } as unknown as ClipboardEvent;

    component.onPaste(clipboardEvent);
    expect(preventDefault).not.toHaveBeenCalled();

    component.formGroup.controls.mode.setValue('imageToVideo');
    component.onPaste(clipboardEvent);

    expect(preventDefault).toHaveBeenCalledTimes(1);
    expect(component.sourceImageFile).toBe(sourceImage);
  });

  it('passes compendium context through the prompt-generation dialogs', () => {
    const requestClose = new Subject<GenerateTextRequestDto>();
    const resultClose = new Subject<string | 'back' | undefined>();
    const requestDialog = dialogWithClose(requestClose);
    const resultDialog = dialogWithClose(resultClose);
    dialogService.open.and.returnValues(requestDialog, resultDialog);
    promptService.getPrompts.and.returnValue(
      of([
        prompt(
          'image-prompt',
          PromptType.CreateCompendiumRecordImageGenerationPrompt,
        ),
      ]),
    );
    component = createComponent(
      {
        enablePromptGeneration: true,
        compendiumId: 'compendium-id',
        compendiumRecordId: 'record-id',
      },
      true,
    );

    component.openGeneratePromptDialog();

    const requestDialogConfig = dialogService.open.calls.argsFor(0)[1] as {
      data: { contextInfo: GenerateTextRequestDto['contextInfo'] };
    };
    expect(dialogService.open.calls.argsFor(0)[0]).toBe(GenerateTextComponent);
    expect(requestDialogConfig.data.contextInfo).toEqual(
      jasmine.objectContaining({
        compendiumId: 'compendium-id',
        compendiumRecordId: 'record-id',
      }),
    );

    const request = {
      model: 'text-model',
      promptId: 'image-prompt',
      contextInfo: requestDialogConfig.data.contextInfo,
    } as GenerateTextRequestDto;
    requestClose.next(request);

    const resultDialogConfig = dialogService.open.calls.argsFor(1)[1] as {
      data: { request: GenerateTextRequestDto };
    };
    expect(dialogService.open.calls.argsFor(1)[0]).toBe(
      GenerateTextResultComponent,
    );
    expect(resultDialogConfig.data.request).toBe(request);

    resultClose.next('  A cinematic castle  ');

    expect(component.formGroup.controls.prompt.value).toBe(
      'A cinematic castle',
    );
    expect(component.formGroup.dirty).toBeTrue();
  });

  it('reopens prompt configuration when the result dialog goes back', () => {
    const requestClose = new Subject<GenerateTextRequestDto>();
    const resultClose = new Subject<string | 'back' | undefined>();
    const reopenedClose = new Subject<GenerateTextRequestDto>();
    dialogService.open.and.returnValues(
      dialogWithClose(requestClose),
      dialogWithClose(resultClose),
      dialogWithClose(reopenedClose),
    );
    promptService.getPrompts.and.returnValue(
      of([
        prompt(
          'image-prompt',
          PromptType.CreateCompendiumRecordImageGenerationPrompt,
        ),
      ]),
    );
    component = createComponent(
      {
        enablePromptGeneration: true,
        compendiumId: 'compendium-id',
        compendiumRecordId: 'record-id',
      },
      true,
    );

    component.openGeneratePromptDialog();
    const requestDialogConfig = dialogService.open.calls.argsFor(0)[1] as {
      data: { contextInfo: GenerateTextRequestDto['contextInfo'] };
    };
    requestClose.next({
      model: 'text-model',
      promptId: 'image-prompt',
      contextInfo: requestDialogConfig.data.contextInfo,
    } as GenerateTextRequestDto);
    resultClose.next('back');

    expect(dialogService.open.calls.count()).toBe(3);
    expect(dialogService.open.calls.argsFor(2)[0]).toBe(GenerateTextComponent);
  });

  it('releases generated and source previews and closes child dialogs on destroy', () => {
    const childClose = new Subject<GenerateTextRequestDto>();
    const childDialog = dialogWithClose(childClose);
    dialogService.open.and.returnValue(childDialog);
    component.promptGenerationPrompts = [
      prompt(
        'image-prompt',
        PromptType.CreateCompendiumRecordImageGenerationPrompt,
      ),
    ];
    component.data = {
      enablePromptGeneration: true,
      compendiumId: 'compendium-id',
      compendiumRecordId: 'record-id',
    };
    component.formGroup.setValue({
      prompt: 'A castle',
      model: 'image-model',
      mode: 'image',
    });
    component.generateMedia();
    component.formGroup.controls.mode.setValue('imageToVideo');
    const pasteEvent = {
      clipboardData: {
        items: [
          {
            kind: 'file',
            type: 'image/png',
            getAsFile: () => sourceImage,
          },
        ],
      },
      preventDefault: () => undefined,
    } as unknown as ClipboardEvent;
    component.onPaste(pasteEvent);
    component.openGeneratePromptDialog();

    component.ngOnDestroy();

    expect(
      (childDialog.close as jasmine.Spy),
    ).toHaveBeenCalledTimes(1);
    expect(revokeObjectUrlSpy).toHaveBeenCalledWith('blob:generated');
    expect(revokeObjectUrlSpy).toHaveBeenCalledWith('blob:source');
  });
});
