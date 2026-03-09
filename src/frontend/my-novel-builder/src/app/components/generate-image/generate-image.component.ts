import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import {
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { GenerateImageService } from '../../services/generate-image.service';
import { LocalStorageService } from '../../services/local-storage.service';
import {
  DialogService,
  DynamicDialogConfig,
  DynamicDialogRef,
} from 'primeng/dynamicdialog';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { ToastrModule, ToastrService } from 'ngx-toastr';
import { HttpEvent, HttpEventType } from '@angular/common/http';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { TextareaModule } from 'primeng/textarea';
import { ButtonModule } from 'primeng/button';
import { PromptService } from '../../services/prompt.service';
import { PromptType } from '../../types/enums/prompt-type';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import {
  CompendiumTextGenerationType,
  CreateCompendiumRecordImageGenerationPromptContextInfoDto,
  GenerateTextRequestDto,
} from '../../types/dtos/generate/generate-text-request.dto';
import {
  GenerateTextComponent,
  GenerateTextComponentData,
} from '../generate-text/generate-text.component';
import {
  GenerateTextResultComponent,
  GenerateTextResultComponentData,
} from '../generate-text-result/generate-text-result.component';
import { ModelSelectComponent } from '../model-select/model-select.component';

export interface GenerateImageComponentData {
  enablePromptGeneration?: boolean;
  compendiumId?: string;
  compendiumRecordId?: string;
}

@Component({
  selector: 'app-generate-image',
  standalone: true,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    ToastrModule,
    TextareaModule,
    ButtonModule,
    ModelSelectComponent,
  ],
  templateUrl: './generate-image.component.html',
  styleUrl: './generate-image.component.scss',
})
export class GenerateImageComponent implements OnInit, OnDestroy {
  private readonly storageContext = 'generate';

  dialogRef = inject(DynamicDialogRef);
  config = inject(DynamicDialogConfig);

  readonly generateImageService: GenerateImageService =
    inject(GenerateImageService);
  readonly localStorageService: LocalStorageService =
    inject(LocalStorageService);
  readonly toastrService: ToastrService = inject(ToastrService);
  readonly sanitizer: DomSanitizer = inject(DomSanitizer);
  readonly promptService: PromptService = inject(PromptService);
  readonly dialogService: DialogService = inject(DialogService);

  data: GenerateImageComponentData = {};
  promptGenerationPrompts: PromptDto[] = [];
  promptGenerationDialogRef: DynamicDialogRef | null = null;
  isLoadingPromptGenerationPrompts = false;

  formGroup = new FormGroup({
    prompt: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    model: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  imageBlob: Blob | null = null;
  imagePreview: SafeUrl | null = null;
  isGenerating = false;

  constructor() {
    this.data = (this.config.data ?? {}) as GenerateImageComponentData;

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

  ngOnInit(): void {
    if (this.isPromptGenerationEnabled()) {
      this.getPromptGenerationPrompts();
    }
  }

  ngOnDestroy(): void {
    this.promptGenerationDialogRef?.close();
  }

  generateImage(): void {
    if (this.formGroup.invalid) {
      this.toastrService.error('Please fill out all fields');
      return;
    }

    const { prompt, model } = this.formGroup.getRawValue();

    if (!prompt.trim() || !model.trim()) {
      this.toastrService.error('Please fill out all fields');
      return;
    }

    // Save the prompt and model
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

    this.isGenerating = true;

    this.generateImageService
      .generateImage({
        modelId: model,
        prompt: prompt,
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

  canGeneratePrompt(): boolean {
    return (
      this.isPromptGenerationEnabled() && this.promptGenerationPrompts.length > 0
    );
  }

  openGeneratePromptDialog(): void {
    if (!this.canGeneratePrompt()) {
      return;
    }

    const { compendiumId, compendiumRecordId } = this.data;
    if (!compendiumId || !compendiumRecordId) {
      this.toastrService.error('Missing compendium context for prompt generation');
      return;
    }

    this.promptGenerationDialogRef = this.dialogService.open(
      GenerateTextComponent,
      {
        header: 'Generate Prompt',
        width: '50vw',
        contentStyle: { overflow: 'auto' },
        baseZIndex: 11000,
        modal: true,
        closable: true,
        closeOnEscape: true,
        dismissableMask: true,
        data: <GenerateTextComponentData>{
          prompts: this.promptGenerationPrompts,
          contextInfo: <CreateCompendiumRecordImageGenerationPromptContextInfoDto>{
            $type:
              CompendiumTextGenerationType.CreateCompendiumRecordImageGenerationPrompt,
            compendiumId: compendiumId,
            compendiumRecordId: compendiumRecordId,
            instructions: null,
          },
          instructionsRequired: false,
          showInstructions: true,
        },
      },
    );

    this.promptGenerationDialogRef?.onClose.subscribe(
      (request: GenerateTextRequestDto) => {
        if (request) {
          this.openGeneratePromptResultDialog(request);
        }
      },
    );
  }

  private openGeneratePromptResultDialog(request: GenerateTextRequestDto): void {
    this.promptGenerationDialogRef = this.dialogService.open(
      GenerateTextResultComponent,
      {
        header: 'Generate Prompt',
        width: '50vw',
        contentStyle: { overflow: 'auto' },
        baseZIndex: 11000,
        modal: true,
        closable: true,
        closeOnEscape: true,
        dismissableMask: true,
        data: <GenerateTextResultComponentData>{
          request,
          textToReplace: '',
        },
      },
    );

    this.promptGenerationDialogRef?.onClose.subscribe(
      (result: string | 'back' | undefined) => {
        if (result === 'back') {
          this.openGeneratePromptDialog();
        } else if (result && result.trim() !== '') {
          this.formGroup.patchValue({ prompt: result.trim() });
          this.formGroup.markAsDirty();
        }
      },
    );
  }

  private getPromptGenerationPrompts(): void {
    this.isLoadingPromptGenerationPrompts = true;

    this.promptService.getPrompts().subscribe({
      next: (prompts) => {
        this.promptGenerationPrompts = prompts.filter(
          (p) =>
            p.type === PromptType.CreateCompendiumRecordImageGenerationPrompt,
        );

        if (this.promptGenerationPrompts.length === 0) {
          this.toastrService.warning(
            'No prompts are available for image prompt generation',
          );
        }
      },
      error: () => {
        this.toastrService.error('Failed to load prompt-generation prompts');
      },
      complete: () => {
        this.isLoadingPromptGenerationPrompts = false;
      },
    });
  }

  isPromptGenerationEnabled(): boolean {
    return (
      this.data.enablePromptGeneration === true &&
      !!this.data.compendiumId &&
      !!this.data.compendiumRecordId
    );
  }
}
