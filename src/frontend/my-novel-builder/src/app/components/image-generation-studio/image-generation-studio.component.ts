import { HttpEventType } from '@angular/common/http';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import {
  Component,
  EventEmitter,
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
import { forkJoin } from 'rxjs';
import { filter, map, take } from 'rxjs/operators';
import { ModelSelectComponent } from '../model-select/model-select.component';
import { GenerateImageService } from '../../services/generate-image.service';
import { LocalStorageService } from '../../services/local-storage.service';
import { MediaLibraryService } from '../../services/media-library.service';
import { ImageGenRequestDto } from '../../types/dtos/generate/image-gen-request.dto';
import { MediaFolderDto } from '../../types/dtos/media-library/media-folder.dto';
import { LocalStorageKey } from '../../types/enums/local-storage-key';

interface GeneratedStudioImage {
  id: string;
  blob: Blob;
  previewUrl: SafeUrl;
  objectUrl: string;
  isSaving: boolean;
  isSaved: boolean;
  savedFileName: string | null;
}

@Component({
  selector: 'app-image-generation-studio',
  standalone: true,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    TextareaModule,
    SelectModule,
    ButtonModule,
    ModelSelectComponent,
  ],
  templateUrl: './image-generation-studio.component.html',
  styleUrl: './image-generation-studio.component.scss',
})
export class ImageGenerationStudioComponent implements OnChanges, OnDestroy {
  private readonly storageContext = 'generate';
  private generationTimerId: ReturnType<typeof setInterval> | null = null;
  private generationStartedAt: number | null = null;
  private readonly generateImageService = inject(GenerateImageService);
  private readonly localStorageService = inject(LocalStorageService);
  private readonly mediaLibraryService = inject(MediaLibraryService);
  private readonly toastrService = inject(ToastrService);
  private readonly sanitizer = inject(DomSanitizer);

  @Input() folder: MediaFolderDto | null = null;
  @Output() mediaSaved = new EventEmitter<void>();

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
    batchSize: new FormControl<number>(4, {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  generatedImages: GeneratedStudioImage[] = [];
  isGenerating = false;
  generationElapsedSeconds = 0;
  lastGenerationDurationSeconds: number | null = null;
  private previousFolderId: string | null = null;

  constructor() {
    const prompt =
      this.localStorageService.getNestedStringForKey(
        LocalStorageKey.LastImagePromptByContext,
        this.storageContext,
      ) ??
      this.localStorageService.getStringForKey(LocalStorageKey.LastImagePrompt);

    if (prompt !== null && prompt.trim() !== '') {
      this.formGroup.patchValue({
        prompt,
      });
      this.formGroup.markAsDirty();
    }
  }

  get generateButtonLabel(): string {
    if (!this.isGenerating) {
      return 'Generate';
    }

    return `Generating (${this.generationElapsedSeconds}s)`;
  }

  get generationStatusLabel(): string | null {
    if (this.isGenerating || this.lastGenerationDurationSeconds === null) {
      return null;
    }

    return `Generation took ${this.lastGenerationDurationSeconds}s`;
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (
      changes['folder'] &&
      !changes['folder'].firstChange &&
      this.folder?.id !== this.previousFolderId
    ) {
      this.clearGeneratedImages();
    }

    if (changes['folder']) {
      this.previousFolderId = this.folder?.id ?? null;
    }
  }

  ngOnDestroy(): void {
    this.stopGenerationTimer();
    this.clearGeneratedImages();
  }

  generateImages(): void {
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

    this.localStorageService.setNestedStringForKey(
      LocalStorageKey.LastImagePromptByContext,
      this.storageContext,
      prompt,
    );
    this.localStorageService.setNestedStringForKey(
      LocalStorageKey.LastImageModelByContext,
      this.storageContext,
      model,
    );

    const request: ImageGenRequestDto = {
      modelId: model,
      prompt,
      width: 832,
      height: 1248,
    };

    this.isGenerating = true;
    this.lastGenerationDurationSeconds = null;
    this.startGenerationTimer();
    this.clearGeneratedImages();

    forkJoin(
      Array.from({ length: batchSize }, () => this.generateImageBlob(request)),
    ).subscribe({
      next: (blobs) => {
        this.generatedImages = blobs.map((blob, index) =>
          this.createGeneratedImage(blob, index),
        );
      },
      error: (error) => {
        console.error('Image generation failed', error);
        this.toastrService.error('Image generation failed.');
        this.isGenerating = false;
        this.stopGenerationTimer();
      },
      complete: () => {
        this.isGenerating = false;
        this.stopGenerationTimer();
      },
    });
  }

  saveGeneratedImage(image: GeneratedStudioImage, index: number): void {
    if (this.folder === null || image.isSaving || image.isSaved) {
      return;
    }

    const fileName = this.buildGeneratedFileName(index, image.blob.type);
    const file = new File([image.blob], fileName, {
      type: image.blob.type || 'image/png',
    });

    image.isSaving = true;

    this.mediaLibraryService.uploadMedia(this.folder.id, fileName, file).subscribe({
      next: () => {
        image.isSaving = false;
        image.isSaved = true;
        image.savedFileName = fileName;
        this.toastrService.success('Media uploaded.');
        this.mediaSaved.emit();
      },
      error: () => {
        image.isSaving = false;
      },
    });
  }

  private generateImageBlob(request: ImageGenRequestDto) {
    return this.generateImageService.generateImage(request).pipe(
      filter((event) => event.type === HttpEventType.Response),
      map((event) => event.body),
      filter((body): body is Blob => body instanceof Blob),
      take(1),
    );
  }

  private createGeneratedImage(blob: Blob, index: number): GeneratedStudioImage {
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

  private clearGeneratedImages(): void {
    for (const image of this.generatedImages) {
      URL.revokeObjectURL(image.objectUrl);
    }

    this.generatedImages = [];
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

  private buildGeneratedFileName(index: number, mimeType: string): string {
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

    return `generated-image-${timestamp}-${index + 1}.${this.extensionForMimeType(
      mimeType,
    )}`;
  }

  private extensionForMimeType(mimeType: string): string {
    switch (mimeType) {
      case 'image/jpeg':
        return 'jpg';
      case 'image/webp':
        return 'webp';
      case 'image/gif':
        return 'gif';
      default:
        return 'png';
    }
  }
}
