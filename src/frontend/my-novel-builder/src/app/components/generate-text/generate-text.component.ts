import { Component, inject } from '@angular/core';
import {
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import {
  DialogService,
  DynamicDialogConfig,
  DynamicDialogRef,
} from 'primeng/dynamicdialog';
import {
  GenerateTextRequestDto,
  TextGenerationContextInfoDto,
} from '../../types/dtos/generate/generate-text-request.dto';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { LocalStorageService } from '../../services/local-storage.service';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { TextareaModule } from 'primeng/textarea';
import { ButtonModule } from 'primeng/button';
import { GenerateTextPreviewComponent } from '../generate-text-preview/generate-text-preview.component';
import { PromptSelectComponent } from '../prompt-select/prompt-select.component';
import { ModelSelectComponent } from '../model-select/model-select.component';

export interface GenerateTextComponentData {
  prompts: PromptDto[];
  contextInfo: TextGenerationContextInfoDto;
  instructionsRequired: boolean;
  showInstructions?: boolean;
  initialPromptId?: string;
  initialModel?: string;
  initialInstructions?: string;
  storageContext?: string;
}

@Component({
  selector: 'app-generate-text',
  standalone: true,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    TextareaModule,
    ButtonModule,
    PromptSelectComponent,
    ModelSelectComponent,
  ],
  providers: [DialogService],
  templateUrl: './generate-text.component.html',
  styleUrl: './generate-text.component.scss',
})
export class GenerateTextComponent {
  config = inject(DynamicDialogConfig);
  dialogRef = inject(DynamicDialogRef);
  private dialogService = inject(DialogService);

  data!: GenerateTextComponentData;
  instructionsRequired = false;
  showInstructions = false;
  storageContext = '';
  readonly localStorageService: LocalStorageService =
    inject(LocalStorageService);

  formGroup = new FormGroup({
    promptId: new FormControl('', [Validators.required]),
    model: new FormControl('', [Validators.required]),
    instructions: new FormControl(''),
  });

  constructor() {
    this.data = this.config.data as GenerateTextComponentData;

    if (this.data.prompts.length === 0) {
      throw new Error('No prompts provided');
    }

    this.storageContext = this.data.storageContext ?? this.data.prompts[0].type;

    // Get the most recent instructions for the prompt type
    const promptType = this.data.prompts[0].type;
    const instructions = this.localStorageService.getNestedStringForKey(
      LocalStorageKey.RecentInstructions,
      promptType,
    );

    if (instructions !== null) {
      this.formGroup.patchValue({ instructions });
    }

    this.formGroup.patchValue({
      promptId: this.data.prompts[0].id,
    });

    const promptId = this.localStorageService.getNestedStringForKey(
      LocalStorageKey.RecentPrompts,
      promptType,
    );

    if (promptId !== null) {
      this.formGroup.patchValue({ promptId: promptId });
    }

    this.instructionsRequired = this.data.instructionsRequired;
    this.showInstructions =
      this.data.showInstructions ?? this.data.instructionsRequired;

    if (!this.showInstructions) {
      this.formGroup.get('instructions')!.disable();
    } else if (this.instructionsRequired) {
      // Add the validators
      this.formGroup.get('instructions')!.setValidators([Validators.required]);
    } else {
      this.formGroup.get('instructions')!.clearValidators();
    }

    this.formGroup.get('instructions')!.updateValueAndValidity();

    if (this.data.initialPromptId) {
      this.formGroup.patchValue({ promptId: this.data.initialPromptId });
    }

    if (this.data.initialModel) {
      this.formGroup.patchValue({ model: this.data.initialModel });
    }

    if (this.data.initialInstructions !== undefined) {
      this.formGroup.patchValue({
        instructions: this.data.initialInstructions,
      });
    }
  }

  accept(): void {
    if (this.formGroup.invalid) {
      return;
    }

    const request = this.buildRequest();
    if (request === null) {
      return;
    }

    this.saveRecentModelForContext();

    // Save the instructions for the prompt type
    const promptType = this.data.prompts[0].type;
    const instructions = this.formGroup.get('instructions')!.value;

    if (instructions !== null) {
      this.localStorageService.setNestedStringForKey(
        LocalStorageKey.RecentInstructions,
        promptType,
        instructions,
      );
    }

    // Save the prompt id
    const promptId = this.formGroup.get('promptId')!.value;
    if (promptId !== null) {
      this.localStorageService.setNestedStringForKey(
        LocalStorageKey.RecentPrompts,
        promptType,
        promptId,
      );
    }

    this.dialogRef.close(request);
  }

  openPreviewDialog(): void {
    if (this.formGroup.invalid) {
      return;
    }

    const request = this.buildRequest();
    if (request === null) {
      return;
    }

    this.saveRecentModelForContext();

    this.dialogService.open(GenerateTextPreviewComponent, {
      header: 'Prompt Preview',
      width: '50vw',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      modal: true,
      closable: true,
      closeOnEscape: true,
      dismissableMask: true,
      focusOnShow: false,
      data: {
        request,
      },
    });
  }

  private buildRequest(): GenerateTextRequestDto | null {
    const promptId = this.formGroup.get('promptId')!.value;
    const model = this.formGroup.get('model')!.value;

    if (promptId === null || model === null) {
      return null;
    }

    let contextInfo: TextGenerationContextInfoDto = this.data.contextInfo;
    if ('instructions' in contextInfo) {
      contextInfo = <TextGenerationContextInfoDto>(<unknown>{
        ...contextInfo,
        instructions: this.formGroup.get('instructions')!.value,
      });
    }

    return {
      promptId,
      model,
      contextInfo,
    };
  }

  // TODO: There is a better way to do this
  getPromptName(promptId: string): string {
    const prompt = this.data.prompts.find((p) => p.id === promptId);
    return prompt ? prompt.name : '';
  }

  private saveRecentModelForContext(): void {
    const model = this.formGroup.get('model')!.value;

    if (!this.storageContext || model === null || model.trim() === '') {
      return;
    }

    this.localStorageService.pushNestedRecentStringForKey(
      LocalStorageKey.RecentTextModelsByContext,
      this.storageContext,
      model,
      5,
    );
  }
}
