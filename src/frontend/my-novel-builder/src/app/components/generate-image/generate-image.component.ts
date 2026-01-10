import { Component, inject } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { GenerateImageService } from '../../services/generate-image.service';
import { LocalStorageService } from '../../services/local-storage.service';
import { DynamicDialogRef } from 'primeng/dynamicdialog';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { ToastrModule, ToastrService } from 'ngx-toastr';
import { HttpEvent, HttpEventType } from '@angular/common/http';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { TextareaModule } from 'primeng/textarea';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-generate-image',
  standalone: true,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    ToastrModule,
    TextareaModule,
    ButtonModule,
  ],
  templateUrl: './generate-image.component.html',
  styleUrl: './generate-image.component.scss'
})
export class GenerateImageComponent {
  dialogRef = inject(DynamicDialogRef);

  models: string[] = ['z-image/turbo']; // TODO: Get models from API
  readonly generateImageService: GenerateImageService = inject(GenerateImageService);
  readonly localStorageService: LocalStorageService = inject(LocalStorageService);
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
    const prompt =
      this.localStorageService.getStringForKey(
        LocalStorageKey.LastImagePrompt);

    this.formGroup.patchValue({
      model: this.models[0],
    });

    if (prompt !== null && prompt.trim() !== '') {
      this.formGroup.patchValue({
        prompt,
      });
      this.formGroup.markAsDirty();
    }
  }

  generateImage(): void {
    if (this.formGroup.invalid) {
      this.toastrService.error('Please fill out all fields');
      return;
    }

    // Save the prompt
    this.localStorageService.setStringForKey(
      LocalStorageKey.LastImagePrompt,
      this.formGroup.get('prompt')!.value!);

    this.isGenerating = true;

    this.generateImageService.generateImage({
      modelId: this.formGroup.get('model')!.value!,
      prompt: this.formGroup.get('prompt')!.value!,
      width: 832,
      height: 1248,
    }).subscribe({
      next: (event: HttpEvent<Blob>) => {
        if (event.type === HttpEventType.Response) {
          this.imageBlob = event.body;
  
          if (event.body !== null) {
            const objectURL = URL.createObjectURL(event.body);
            this.imagePreview = this.sanitizer.bypassSecurityTrustUrl(
              objectURL);
          }
        }
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