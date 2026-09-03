import { Component, OnDestroy, inject } from '@angular/core';
import {
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { GenerateImageService } from '../../services/generate-image.service';
import { LocalStorageService } from '../../services/local-storage.service';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { ToastrModule, ToastrService } from 'ngx-toastr';
import { HttpEvent, HttpEventType } from '@angular/common/http';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { TextareaModule } from 'primeng/textarea';
import { ButtonModule } from 'primeng/button';
import { ModelSelectComponent } from '../model-select/model-select.component';

export interface EditImageComponentData {
  image?: File;
  width?: number;
  height?: number;
}

@Component({
  selector: 'app-edit-image',
  standalone: true,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    ToastrModule,
    TextareaModule,
    ButtonModule,
    ModelSelectComponent,
  ],
  templateUrl: './edit-image.component.html',
  styleUrl: './edit-image.component.scss',
})
export class EditImageComponent implements OnDestroy {
  private readonly storageContext = 'edit';
  private generationTimerId: ReturnType<typeof setInterval> | null = null;
  private generationStartedAt: number | null = null;
  private originalImagePreviewObjectUrl: string | null = null;
  private imagePreviewObjectUrl: string | null = null;

  dialogRef = inject(DynamicDialogRef);
  config = inject(DynamicDialogConfig);

  readonly generateImageService: GenerateImageService =
    inject(GenerateImageService);
  readonly localStorageService: LocalStorageService =
    inject(LocalStorageService);
  readonly toastrService: ToastrService = inject(ToastrService);
  readonly sanitizer: DomSanitizer = inject(DomSanitizer);

  formGroup = new FormGroup({
    prompt: new FormControl('', [Validators.required]),
    model: new FormControl('', [Validators.required]),
  });

  imageBlob: Blob | null = null;
  imagePreview: SafeUrl | null = null;
  isGenerating = false;
  generationElapsedSeconds = 0;
  lastGenerationDurationSeconds: number | null = null;

  originalImage: File | null = null;
  originalImagePreview: SafeUrl | null = null;
  width = 0;
  height = 0;

  constructor() {
    const data = (this.config.data ?? {}) as EditImageComponentData;
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

    this.width = data.width ?? 0;
    this.height = data.height ?? 0;

    if (data.image) {
      this.originalImage = data.image;
      const objectURL = URL.createObjectURL(this.originalImage);
      this.originalImagePreviewObjectUrl = objectURL;
      this.originalImagePreview =
        this.sanitizer.bypassSecurityTrustUrl(objectURL);

      if (this.width === 0 || this.height === 0) {
        // Fall back to the original image dimensions when no target size is provided.
        const img = new Image();
        img.onload = () => {
          this.width = img.width;
          this.height = img.height;
        };
        img.src = objectURL;
      }
    }
  }

  ngOnDestroy(): void {
    this.stopGenerationTimer();
    this.revokePreviewUrls();
  }

  editImage(): void {
    if (this.isGenerating) {
      return;
    }

    if (this.formGroup.invalid || !this.originalImage) {
      this.toastrService.error('Please fill out all fields');
      return;
    }

    // Save the prompt and model
    this.localStorageService.setNestedStringForKey(
      LocalStorageKey.LastImagePromptByContext,
      this.storageContext,
      this.formGroup.get('prompt')!.value!,
    );
    this.localStorageService.setNestedStringForKey(
      LocalStorageKey.LastImageModelByContext,
      this.storageContext,
      this.formGroup.get('model')!.value!,
    );

    this.isGenerating = true;
    this.lastGenerationDurationSeconds = null;
    this.startGenerationTimer();

    this.generateImageService
      .editImage(this.originalImage, {
        modelId: this.formGroup.get('model')!.value!,
        prompt: this.formGroup.get('prompt')!.value!,
        width: this.width,
        height: this.height,
      })
      .subscribe({
        next: (event: HttpEvent<Blob>) => {
          if (event.type === HttpEventType.Response) {
            this.imageBlob = event.body;

            if (event.body !== null) {
              if (this.imagePreviewObjectUrl !== null) {
                URL.revokeObjectURL(this.imagePreviewObjectUrl);
              }

              const objectURL = URL.createObjectURL(event.body);
              this.imagePreviewObjectUrl = objectURL;
              this.imagePreview =
                this.sanitizer.bypassSecurityTrustUrl(objectURL);
            }
          }
        },
        error: (err) => {
          console.error('Image editing failed', err);
          this.toastrService.error('Image editing failed');
          this.isGenerating = false;
          this.stopGenerationTimer();
        },
        complete: () => {
          this.isGenerating = false;
          this.stopGenerationTimer();
        },
      });
  }

  get editButtonLabel(): string {
    return this.isGenerating
      ? `Editing (${this.generationElapsedSeconds}s)`
      : 'Edit';
  }

  get generationStatusLabel(): string | null {
    if (this.isGenerating) {
      return null;
    }

    if (this.lastGenerationDurationSeconds === null) {
      return null;
    }

    return `Editing took ${this.lastGenerationDurationSeconds}s`;
  }

  accept(): void {
    this.dialogRef.close(this.imageBlob);
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

  private revokePreviewUrls(): void {
    if (this.originalImagePreviewObjectUrl !== null) {
      URL.revokeObjectURL(this.originalImagePreviewObjectUrl);
      this.originalImagePreviewObjectUrl = null;
    }

    if (this.imagePreviewObjectUrl !== null) {
      URL.revokeObjectURL(this.imagePreviewObjectUrl);
      this.imagePreviewObjectUrl = null;
    }
  }
}
