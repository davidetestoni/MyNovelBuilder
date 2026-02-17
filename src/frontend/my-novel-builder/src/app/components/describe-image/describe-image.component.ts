import { Component, OnInit, inject } from '@angular/core';
import {
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { PromptService } from '../../services/prompt.service';
import { GenerateTextService } from '../../services/generate-text.service';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { PromptType } from '../../types/enums/prompt-type';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { ButtonModule } from 'primeng/button';
import { LocalStorageService } from '../../services/local-storage.service';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { ToastrService } from 'ngx-toastr';
import { DescribeImageRequestDto } from '../../types/dtos/generate/describe-image-request.dto';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';

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
    SelectModule,
    TextareaModule,
    ButtonModule,
  ],
  templateUrl: './describe-image.component.html',
  styleUrl: './describe-image.component.scss',
})
export class DescribeImageComponent implements OnInit {
  config = inject(DynamicDialogConfig);
  dialogRef = inject(DynamicDialogRef);

  readonly promptService: PromptService = inject(PromptService);
  readonly generateTextService: GenerateTextService = inject(GenerateTextService);
  readonly localStorageService: LocalStorageService = inject(LocalStorageService);
  readonly toastrService: ToastrService = inject(ToastrService);
  readonly sanitizer: DomSanitizer = inject(DomSanitizer);

  data!: DescribeImageComponentData;
  prompts: PromptDto[] = [];
  models: string[] = [];
  imagePreview: SafeUrl | null = null;

  isLoadingPrompts = false;
  isLoadingModels = false;
  isGenerating = false;
  description: string | null = null;

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

  ngOnInit(): void {
    this.getPrompts();
    this.getModels();
  }

  getPrompts(): void {
    this.isLoadingPrompts = true;

    this.promptService.getPrompts().subscribe({
      next: (prompts) => {
        this.prompts = prompts.filter((p) => p.type === PromptType.DescribeImage);

        if (this.prompts.length === 0) {
          this.toastrService.warning('No prompts are available for image description');
          return;
        }

        const selectedPromptId = this.formGroup.get('promptId')!.value;
        const hasSelectedPrompt =
          selectedPromptId !== null && this.prompts.some((p) => p.id === selectedPromptId);

        if (!hasSelectedPrompt) {
          this.formGroup.patchValue({ promptId: this.prompts[0].id });
        }
      },
      error: () => {
        this.toastrService.error('Failed to load prompts');
      },
      complete: () => {
        this.isLoadingPrompts = false;
      },
    });
  }

  getModels(): void {
    this.isLoadingModels = true;

    this.generateTextService.getAvailableVisionModels().subscribe({
      next: (models) => {
        this.models = models;

        if (this.models.length === 0) {
          this.toastrService.warning('No vision-capable text models are available');
          return;
        }

        this.formGroup.patchValue({ model: this.models[0] });
      },
      error: () => {
        this.toastrService.error('Failed to load models');
      },
      complete: () => {
        this.isLoadingModels = false;
      },
    });
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
}
