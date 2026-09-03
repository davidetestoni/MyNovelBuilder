import { Component, OnDestroy, inject } from '@angular/core';
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
import {
  DescribeCompendiumImageRequestDto,
  DescribeImageRequestDto,
} from '../../types/dtos/generate/describe-image-request.dto';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { PromptSelectComponent } from '../prompt-select/prompt-select.component';
import { ModelSelectComponent } from '../model-select/model-select.component';

export interface DescribeImageComponentData {
  image: File;
  compendiumId?: string | null;
  promptType?: PromptType;
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
export class DescribeImageComponent implements OnDestroy {
  private readonly imagePreviewObjectUrl: string;

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

    this.imagePreviewObjectUrl = URL.createObjectURL(this.data.image);
    this.imagePreview = this.sanitizer.bypassSecurityTrustUrl(
      this.imagePreviewObjectUrl,
    );

    const instructions = this.localStorageService.getNestedStringForKey(
      LocalStorageKey.RecentInstructions,
      this.selectedPromptType,
    );

    if (instructions !== null) {
      this.formGroup.patchValue({ instructions });
    }

    const promptId = this.localStorageService.getNestedStringForKey(
      LocalStorageKey.RecentPrompts,
      this.selectedPromptType,
    );

    if (promptId !== null) {
      this.formGroup.patchValue({ promptId });
    }
  }

  get selectedPromptType(): PromptType {
    return this.data.promptType ?? PromptType.DescribeImage;
  }

  ngOnDestroy(): void {
    URL.revokeObjectURL(this.imagePreviewObjectUrl);
  }

  describeImage(): void {
    if (this.isGenerating) {
      return;
    }

    if (this.formGroup.invalid) {
      this.toastrService.error('Please fill out all required fields');
      return;
    }

    if (
      this.selectedPromptType === PromptType.DescribeCompendiumImage &&
      (!this.data.compendiumId || this.data.compendiumId.trim() === '')
    ) {
      this.toastrService.error('Compendium context is required for this prompt type');
      return;
    }

    this.isGenerating = true;
    this.description = null;

    const request: DescribeImageRequestDto = {
      model: this.formGroup.get('model')!.value!,
      promptId: this.formGroup.get('promptId')!.value!,
      instructions: this.formGroup.get('instructions')!.value,
    };

    if (this.selectedPromptType === PromptType.DescribeCompendiumImage) {
      const compendiumRequest: DescribeCompendiumImageRequestDto = {
        ...request,
        compendiumId: this.data.compendiumId!,
      };

      this.generateTextService.describeImage(this.data.image, compendiumRequest).subscribe({
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
      return;
    }

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
      this.selectedPromptType,
      this.formGroup.get('instructions')!.value ?? '',
    );

    this.localStorageService.setNestedStringForKey(
      LocalStorageKey.RecentPrompts,
      this.selectedPromptType,
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
