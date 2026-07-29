import { HttpEventType } from '@angular/common/http';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import {
  Component,
  EventEmitter,
  HostListener,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
  inject,
} from '@angular/core';
import {
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { forkJoin, Subscription } from 'rxjs';
import { filter, map, take } from 'rxjs/operators';
import { GenerateImageService } from '../../services/generate-image.service';
import { LocalStorageService } from '../../services/local-storage.service';
import { MediaLibraryService } from '../../services/media-library.service';
import { GenerateVideoService } from '../../services/generate-video.service';
import { ImageGenRequestDto } from '../../types/dtos/generate/image-gen-request.dto';
import { MediaFolderDto } from '../../types/dtos/media-library/media-folder.dto';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import {
  extractImageFileFromClipboardData,
  readImageFileFromClipboard,
} from '../../utils/clipboard-image';
import {
  createGeneratedMediaFile,
  isVideoMimeType,
} from '../../utils/generated-media';
import { ModelSelectComponent } from '../model-select/model-select.component';

type StudioGenerationMode = 'image' | 'textToVideo' | 'imageToVideo';

interface GeneratedStudioAsset {
  id: string;
  blob: Blob;
  previewUrl: SafeUrl;
  objectUrl: string;
  isSaving: boolean;
  isSaved: boolean;
  savedFileName: string | null;
}

@Component({
  selector: 'app-media-generation-studio',
  standalone: true,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    TextareaModule,
    SelectModule,
    ButtonModule,
    ModelSelectComponent,
  ],
  templateUrl: './media-generation-studio.component.html',
  styleUrl: './media-generation-studio.component.scss',
})
export class MediaGenerationStudioComponent implements OnChanges, OnDestroy {
  private generationTimerId: ReturnType<typeof setInterval> | null = null;
  private generationStartedAt: number | null = null;
  private generationSubscription: Subscription | null = null;
  private readonly generateImageService = inject(GenerateImageService);
  private readonly generateVideoService = inject(GenerateVideoService);
  private readonly localStorageService = inject(LocalStorageService);
  private readonly mediaLibraryService = inject(MediaLibraryService);
  private readonly toastrService = inject(ToastrService);
  private readonly sanitizer = inject(DomSanitizer);

  @Input() folder: MediaFolderDto | null = null;
  @Output() mediaSaved = new EventEmitter<void>();

  readonly generationModeOptions: {
    label: string;
    value: StudioGenerationMode;
  }[] = [
    { label: 'Image', value: 'image' },
    { label: 'Text to Video', value: 'textToVideo' },
    { label: 'Image to Video', value: 'imageToVideo' },
  ];

  readonly batchSizeOptions = [1, 2, 3, 4, 6, 8].map((value) => ({
    label: `${value}`,
    value,
  }));

  readonly formGroup = new FormGroup({
    prompt: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    model: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    mode: new FormControl<StudioGenerationMode>('image', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    batchSize: new FormControl<number>(4, {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  generatedAssets: GeneratedStudioAsset[] = [];
  sourceImageFile: File | null = null;
  sourceImagePreview: SafeUrl | null = null;
  private sourceImageObjectUrl: string | null = null;
  isGenerating = false;
  isPastingSourceImage = false;
  generationElapsedSeconds = 0;
  lastGenerationDurationSeconds: number | null = null;
  private previousFolderId: string | null = null;

  constructor() {
    this.restoreStoredPromptForCurrentMode();

    this.formGroup.controls.mode.valueChanges.subscribe((mode) => {
      this.cancelGeneration();
      this.clearGeneratedAssets();
      this.restoreStoredPromptForCurrentMode();

      if (mode !== 'imageToVideo') {
        this.clearSourceImage();
      }
    });
  }

  get generateButtonLabel(): string {
    if (this.isGenerating) {
      return `Generating ${this.outputLabel} (${this.generationElapsedSeconds}s)`;
    }

    switch (this.selectedMode) {
      case 'textToVideo':
        return 'Generate Video';
      case 'imageToVideo':
        return 'Generate from Image';
      case 'image':
      default:
        return 'Generate';
    }
  }

  get generationStatusLabel(): string | null {
    if (this.isGenerating || this.lastGenerationDurationSeconds === null) {
      return null;
    }

    return `${this.capitalize(this.outputLabel)} generation took ${this.lastGenerationDurationSeconds}s`;
  }

  get selectedMode(): StudioGenerationMode {
    return this.formGroup.controls.mode.getRawValue();
  }

  get selectedCapability(): 'imageGeneration' | 'textToVideo' | 'imageToVideo' {
    switch (this.selectedMode) {
      case 'textToVideo':
        return 'textToVideo';
      case 'imageToVideo':
        return 'imageToVideo';
      case 'image':
      default:
        return 'imageGeneration';
    }
  }

  get outputLabel(): string {
    return this.selectedMode === 'image' ? 'image' : 'video';
  }

  get promptPlaceholder(): string {
    switch (this.selectedMode) {
      case 'textToVideo':
        return 'Describe the video you want to generate...';
      case 'imageToVideo':
        return 'Describe how the uploaded image should move or evolve...';
      case 'image':
      default:
        return 'Describe the image you want to generate...';
    }
  }

  get shouldShowBatchSize(): boolean {
    return this.selectedMode === 'image';
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (
      changes['folder'] &&
      !changes['folder'].firstChange &&
      this.folder?.id !== this.previousFolderId
    ) {
      this.cancelGeneration();
      this.clearGeneratedAssets();
    }

    if (changes['folder']) {
      this.previousFolderId = this.folder?.id ?? null;
    }
  }

  ngOnDestroy(): void {
    this.cancelGeneration();
    this.clearGeneratedAssets();
    this.clearSourceImage();
  }

  generateMedia(): void {
    if (this.isGenerating) {
      return;
    }

    if (this.folder === null) {
      this.toastrService.error('Select a media folder first.');
      return;
    }

    if (this.formGroup.invalid) {
      this.toastrService.error('Please fill out all fields');
      return;
    }

    const { prompt, model, batchSize } = this.formGroup.getRawValue();
    if (!prompt.trim() || !model.trim()) {
      this.toastrService.error('Please fill out all fields');
      return;
    }

    if (this.selectedMode === 'imageToVideo' && this.sourceImageFile === null) {
      this.toastrService.error('Upload a source image first.');
      return;
    }

    this.persistPromptAndModel(prompt, model);

    const request: ImageGenRequestDto = {
      modelId: model,
      prompt,
      width: 832,
      height: 1248,
    };

    this.isGenerating = true;
    this.lastGenerationDurationSeconds = null;
    this.startGenerationTimer();
    this.clearGeneratedAssets();

    const requestCount = this.selectedMode === 'image' ? batchSize : 1;

    const subscription = forkJoin(
      Array.from({ length: requestCount }, () => this.generateAssetBlob(request)),
    ).subscribe({
      next: (blobs) => {
        this.generatedAssets = blobs.map((blob, index) =>
          this.createGeneratedAsset(blob, index),
        );
      },
      error: (error) => {
        console.error('Media generation failed', error);
        this.toastrService.error('Media generation failed.');
        this.isGenerating = false;
        this.stopGenerationTimer();
        this.generationSubscription = null;
      },
      complete: () => {
        this.isGenerating = false;
        this.stopGenerationTimer();
        this.generationSubscription = null;
      },
    });
    this.generationSubscription = subscription.closed ? null : subscription;
  }

  saveGeneratedImage(asset: GeneratedStudioAsset, index: number): void {
    if (this.folder === null || asset.isSaving || asset.isSaved) {
      return;
    }

    const file = createGeneratedMediaFile(
      asset.blob,
      this.buildGeneratedBaseName(index, asset.blob.type),
    );
    const fileName = file.name;

    asset.isSaving = true;

    this.mediaLibraryService.uploadMedia(this.folder.id, fileName, file).subscribe({
      next: () => {
        asset.isSaving = false;
        asset.isSaved = true;
        asset.savedFileName = fileName;
        this.toastrService.success('Media uploaded.');
        this.mediaSaved.emit();
      },
      error: () => {
        asset.isSaving = false;
        this.toastrService.error('Media upload failed.');
      },
    });
  }

  onSourceImageSelected(event: Event): void {
    const input = event.target;
    if (!(input instanceof HTMLInputElement)) {
      return;
    }

    const file = input.files?.[0] ?? null;
    if (file === null) {
      return;
    }

    this.clearGeneratedAssets();
    this.setSourceImage(file);
    input.value = '';
  }

  clearSelectedSourceImage(): void {
    this.clearGeneratedAssets();
    this.clearSourceImage();
  }

  @HostListener('paste', ['$event'])
  onPaste(event: ClipboardEvent): void {
    if (this.selectedMode !== 'imageToVideo') {
      return;
    }

    const file = extractImageFileFromClipboardData(
      event.clipboardData?.items,
      'source-image',
    );
    if (file === null) {
      return;
    }

    event.preventDefault();
    this.applySourceImage(file);
  }

  async pasteSourceImageFromClipboard(): Promise<void> {
    if (this.selectedMode !== 'imageToVideo' || this.isPastingSourceImage) {
      return;
    }

    this.isPastingSourceImage = true;

    try {
      const file = await readImageFileFromClipboard('source-image');
      this.applySourceImage(file);
    } catch (error) {
      console.error('Failed to read clipboard image', error);
      this.toastrService.error(
        error instanceof Error
          ? error.message
          : 'Clipboard image paste failed.',
      );
    } finally {
      this.isPastingSourceImage = false;
    }
  }

  isVideoAsset(asset: GeneratedStudioAsset): boolean {
    return isVideoMimeType(asset.blob.type);
  }

  private get storageContext(): string {
    switch (this.selectedMode) {
      case 'textToVideo':
        return 'generate-video';
      case 'imageToVideo':
        return 'generate-image-to-video';
      case 'image':
      default:
        return 'generate';
    }
  }

  private generateAssetBlob(request: ImageGenRequestDto) {
    const generationRequest =
      this.selectedMode === 'image'
        ? this.generateImageService.generateImage(request)
        : this.selectedMode === 'textToVideo'
          ? this.generateVideoService.generateVideo(request)
          : this.generateVideoService.generateVideoFromImage(
              this.sourceImageFile!,
              request,
            );

    return generationRequest.pipe(
      filter((event) => event.type === HttpEventType.Response),
      map((event) => event.body),
      filter((body): body is Blob => body instanceof Blob),
      take(1),
    );
  }

  private createGeneratedAsset(blob: Blob, index: number): GeneratedStudioAsset {
    const objectUrl = URL.createObjectURL(blob);

    return {
      id: `${Date.now()}-${index}`,
      blob,
      previewUrl: this.sanitizer.bypassSecurityTrustUrl(objectUrl),
      objectUrl,
      isSaving: false,
      isSaved: false,
      savedFileName: null,
    };
  }

  private clearGeneratedAssets(): void {
    for (const asset of this.generatedAssets) {
      URL.revokeObjectURL(asset.objectUrl);
    }

    this.generatedAssets = [];
  }

  private cancelGeneration(): void {
    this.generationSubscription?.unsubscribe();
    this.generationSubscription = null;

    if (this.isGenerating) {
      this.isGenerating = false;
      this.stopGenerationTimer();
    }

    this.generationElapsedSeconds = 0;
    this.lastGenerationDurationSeconds = null;
  }

  private startGenerationTimer(): void {
    this.stopGenerationTimer();
    this.generationStartedAt = Date.now();
    this.generationElapsedSeconds = 0;
    this.generationTimerId = setInterval(() => {
      if (this.generationStartedAt === null) {
        return;
      }

      this.generationElapsedSeconds = Math.floor(
        (Date.now() - this.generationStartedAt) / 1000,
      );
    }, 1000);
  }

  private stopGenerationTimer(): void {
    if (this.generationTimerId !== null) {
      clearInterval(this.generationTimerId);
      this.generationTimerId = null;
    }

    if (this.generationStartedAt !== null) {
      this.lastGenerationDurationSeconds = Math.floor(
        (Date.now() - this.generationStartedAt) / 1000,
      );
      this.generationElapsedSeconds = this.lastGenerationDurationSeconds;
      this.generationStartedAt = null;
    }
  }

  private buildGeneratedBaseName(index: number, mimeType: string): string {
    const now = new Date();
    const timestamp = [
      now.getFullYear(),
      String(now.getMonth() + 1).padStart(2, '0'),
      String(now.getDate()).padStart(2, '0'),
      '-',
      String(now.getHours()).padStart(2, '0'),
      String(now.getMinutes()).padStart(2, '0'),
      String(now.getSeconds()).padStart(2, '0'),
    ].join('');

    return `${isVideoMimeType(mimeType) ? 'generated-video' : 'generated-image'}-${timestamp}-${index + 1}`;
  }

  private setSourceImage(file: File): void {
    this.clearSourceImage();
    this.sourceImageFile = file;
    this.sourceImageObjectUrl = URL.createObjectURL(file);
    this.sourceImagePreview = this.sanitizer.bypassSecurityTrustUrl(
      this.sourceImageObjectUrl,
    );
  }

  private applySourceImage(file: File): void {
    this.clearGeneratedAssets();
    this.setSourceImage(file);
  }

  private clearSourceImage(): void {
    if (this.sourceImageObjectUrl !== null) {
      URL.revokeObjectURL(this.sourceImageObjectUrl);
      this.sourceImageObjectUrl = null;
    }

    this.sourceImageFile = null;
    this.sourceImagePreview = null;
  }

  private capitalize(value: string): string {
    return value.length === 0
      ? value
      : `${value[0].toUpperCase()}${value.slice(1)}`;
  }

  private restoreStoredPromptForCurrentMode(): void {
    const prompt = this.isVideoMode()
      ? this.localStorageService.getNestedStringForKey(
          LocalStorageKey.LastVideoPromptByContext,
          this.storageContext,
        ) ?? this.localStorageService.getStringForKey(LocalStorageKey.LastVideoPrompt)
      : this.localStorageService.getNestedStringForKey(
          LocalStorageKey.LastImagePromptByContext,
          this.storageContext,
        ) ?? this.localStorageService.getStringForKey(LocalStorageKey.LastImagePrompt);

    if (prompt !== null && prompt.trim() !== '') {
      this.formGroup.patchValue({ prompt });
      this.formGroup.markAsDirty();
    }
  }

  private persistPromptAndModel(prompt: string, model: string): void {
    if (this.isVideoMode()) {
      this.localStorageService.setNestedStringForKey(
        LocalStorageKey.LastVideoPromptByContext,
        this.storageContext,
        prompt,
      );
      this.localStorageService.setStringForKey(LocalStorageKey.LastVideoPrompt, prompt);
      this.localStorageService.setNestedStringForKey(
        LocalStorageKey.LastVideoModelByContext,
        this.storageContext,
        model,
      );
      this.localStorageService.setStringForKey(LocalStorageKey.LastVideoModel, model);
      return;
    }

    this.localStorageService.setNestedStringForKey(
      LocalStorageKey.LastImagePromptByContext,
      this.storageContext,
      prompt,
    );
    this.localStorageService.setStringForKey(LocalStorageKey.LastImagePrompt, prompt);
    this.localStorageService.setNestedStringForKey(
      LocalStorageKey.LastImageModelByContext,
      this.storageContext,
      model,
    );
    this.localStorageService.setStringForKey(LocalStorageKey.LastImageModel, model);
  }

  private isVideoMode(): boolean {
    return this.selectedMode === 'textToVideo' || this.selectedMode === 'imageToVideo';
  }
}
