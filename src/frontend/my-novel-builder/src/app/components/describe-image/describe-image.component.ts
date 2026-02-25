import { Component, inject } from '@angular/core';
import {
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { GenerateTextService } from '../../services/generate-text.service';
import { PromptType } from '../../types/enums/prompt-type';
import { TextareaModule } from 'primeng/textarea';
import { ButtonModule } from 'primeng/button';
import { LocalStorageService } from '../../services/local-storage.service';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { ToastrService } from 'ngx-toastr';
import { DescribeImageRequestDto } from '../../types/dtos/generate/describe-image-request.dto';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { PromptSelectComponent } from '../prompt-select/prompt-select.component';
import { ModelSelectComponent } from '../model-select/model-select.component';

export interface DescribeImageComponentData {
  image: File;
  compendiumId: string;
}

@Component({
  selector: 'app-describe-image',
  standalone: true,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    TextareaModule,
    ButtonModule,
    PromptSelectComponent,
    ModelSelectComponent,
  ],
  templateUrl: './describe-image.component.html',
  styleUrl: './describe-image.component.scss',
})
export class DescribeImageComponent {
  PromptType = PromptType;

  config = inject(DynamicDialogConfig);
  dialogRef = inject(DynamicDialogRef);

  readonly generateTextService: GenerateTextService = inject(GenerateTextService);
  readonly localStorageService: LocalStorageService = inject(LocalStorageService);
  readonly toastrService: ToastrService = inject(ToastrService);
  readonly sanitizer: DomSanitizer = inject(DomSanitizer);

  data!: DescribeImageComponentData;
  imagePreview: SafeUrl | null = null;

  isGenerating = false;
  description: string | null = null;
  promptCount = 0;

  formGroup = new FormGroup({
    promptId: new FormControl('', [Validators.required]),
    model: new FormControl('', [Validators.required]),
    instructions: new FormControl(''),
  });

  constructor() {
    this.data = this.config.data as DescribeImageComponentData;

    const objectURL = URL.createObjectURL(this.data.image);
    this.imagePreview = this.sanitizer.bypassSecurityTrustUrl(objectURL);

    const instructions = this.localStorageService.getNestedStringForKey(
      LocalStorageKey.RecentInstructions,
      PromptType.DescribeImage,
    );

    if (instructions !== null) {
      this.formGroup.patchValue({ instructions });
    }

    const promptId = this.localStorageService.getNestedStringForKey(
      LocalStorageKey.RecentPrompts,
      PromptType.DescribeImage,
    );

    if (promptId !== null) {
      this.formGroup.patchValue({ promptId });
    }
  }

  describeImage(): void {
    if (this.formGroup.invalid) {
      this.toastrService.error('Please fill out all required fields');
      return;
    }

    this.isGenerating = true;
    this.description = null;

    const request: DescribeImageRequestDto = {
      model: this.formGroup.get('model')!.value!,
      promptId: this.formGroup.get('promptId')!.value!,
      compendiumId: this.data.compendiumId,
      instructions: this.formGroup.get('instructions')!.value,
    };

    this.generateTextService.describeImage(this.data.image, request).subscribe({
      next: (description) => {
        this.description = description;
      },
      error: () => {
        this.toastrService.error('Failed to describe image');
        this.isGenerating = false;
      },
      complete: () => {
        this.isGenerating = false;
      },
    });
  }

  accept(): void {
    if (this.description === null || this.description.trim() === '') {
      return;
    }

    this.localStorageService.setNestedStringForKey(
      LocalStorageKey.RecentInstructions,
      PromptType.DescribeImage,
      this.formGroup.get('instructions')!.value ?? '',
    );

    this.localStorageService.setNestedStringForKey(
      LocalStorageKey.RecentPrompts,
      PromptType.DescribeImage,
      this.formGroup.get('promptId')!.value!,
    );

    this.dialogRef.close(this.description.trim());
  }

  onPromptOptionsChanged(count: number): void {
    this.promptCount = count;
    if (count === 0) {
      this.toastrService.warning('No prompts are available for image description');
    }
  }
}
