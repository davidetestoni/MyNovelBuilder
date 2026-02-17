import { Component, OnInit, inject } from '@angular/core';
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
import { SelectModule } from 'primeng/select';
import { ImageGenerationModelInfoDto } from '../../types/dtos/generate/image-generation-model-info.dto';

@Component({
  selector: 'app-edit-image',
  standalone: true,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    ToastrModule,
    TextareaModule,
    ButtonModule,
    SelectModule,
  ],
  templateUrl: './edit-image.component.html',
  styleUrl: './edit-image.component.scss',
})
export class EditImageComponent implements OnInit {
  dialogRef = inject(DynamicDialogRef);
  config = inject(DynamicDialogConfig);

  models: ImageGenerationModelInfoDto[] = [];
  modelOptions: { label: string; value: string }[] = [];
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

  originalImage: File | null = null;
  originalImagePreview: SafeUrl | null = null;
  width = 0;
  height = 0;

  constructor() {
    const prompt = this.localStorageService.getStringForKey(
      LocalStorageKey.LastImagePrompt,
    );

    if (prompt !== null && prompt.trim() !== '') {
      this.formGroup.patchValue({
        prompt,
      });
      this.formGroup.markAsDirty();
    }

    if (this.config.data?.image) {
      this.originalImage = this.config.data.image;
      const objectURL = URL.createObjectURL(this.originalImage!);
      this.originalImagePreview =
        this.sanitizer.bypassSecurityTrustUrl(objectURL);

      // We need to get width and height from the image
      const img = new Image();
      img.onload = () => {
        this.width = img.width;
        this.height = img.height;
      };
      img.src = objectURL;
    }
  }

  ngOnInit(): void {
    this.getModels();
  }

  getModels() {
    this.generateImageService.getAvailableModels().subscribe((models) => {
      this.models = models.filter((m) => m.isImageEditor);
      this.modelOptions = this.models.map((m) => ({
        label: m.name,
        value: m.modelId,
      }));

      const lastModel = this.localStorageService.getStringForKey(
        LocalStorageKey.LastImageModel,
      );

      if (lastModel && this.modelOptions.some((o) => o.value === lastModel)) {
        this.formGroup.patchValue({ model: lastModel });
      } else if (this.modelOptions.length > 0) {
        this.formGroup.patchValue({ model: this.modelOptions[0].value });
      }
    });
  }

  editImage(): void {
    if (this.formGroup.invalid || !this.originalImage) {
      this.toastrService.error('Please fill out all fields');
      return;
    }

    // Save the prompt and model
    this.localStorageService.setStringForKey(
      LocalStorageKey.LastImagePrompt,
      this.formGroup.get('prompt')!.value!,
    );
    this.localStorageService.setStringForKey(
      LocalStorageKey.LastImageModel,
      this.formGroup.get('model')!.value!,
    );

    this.isGenerating = true;

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
              const objectURL = URL.createObjectURL(event.body);
              this.imagePreview =
                this.sanitizer.bypassSecurityTrustUrl(objectURL);
            }
          }
        },
        error: (err) => {
          console.error('Image editing failed', err);
          this.toastrService.error('Image editing failed. Please try again.');
          this.isGenerating = false;
        },
        complete: () => {
          this.isGenerating = false;
        },
      });
  }

  accept(): void {
    this.dialogRef.close(this.imageBlob);
  }
}
