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
import { DynamicDialogRef } from 'primeng/dynamicdialog';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { ToastrModule, ToastrService } from 'ngx-toastr';
import { HttpEvent, HttpEventType } from '@angular/common/http';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { TextareaModule } from 'primeng/textarea';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { ImageGenerationModelInfoDto } from '../../types/dtos/generate/image-generation-model-info.dto';

@Component({
  selector: 'app-generate-image',
  standalone: true,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    ToastrModule,
    TextareaModule,
    ButtonModule,
    SelectModule,
  ],
  templateUrl: './generate-image.component.html',
  styleUrl: './generate-image.component.scss',
})
export class GenerateImageComponent implements OnInit {
  dialogRef = inject(DynamicDialogRef);

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
  }

  ngOnInit(): void {
    this.getModels();
  }

  getModels() {
    this.generateImageService.getAvailableModels().subscribe((models) => {
      this.models = models.filter((m) => !m.isImageEditor);
      this.modelOptions = models.map((m) => ({
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

  generateImage(): void {
    if (this.formGroup.invalid) {
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
      .generateImage({
        modelId: this.formGroup.get('model')!.value!,
        prompt: this.formGroup.get('prompt')!.value!,
        width: 832,
        height: 1248,
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
          console.error('Image generation failed', err);
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
