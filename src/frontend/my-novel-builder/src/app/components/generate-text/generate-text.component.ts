import { Component, Inject, OnInit, inject } from '@angular/core';
import {
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import {
  MAT_DIALOG_DATA,
  MatDialogActions,
  MatDialogClose,
  MatDialogRef,
} from '@angular/material/dialog';
import {
  GenerateTextRequestDto,
  TextGenerationContextInfoDto,
} from '../../types/dtos/generate/generate-text-request.dto';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { GenerateService } from '../../services/generate.service';

export interface GenerateTextComponentData {
  prompts: PromptDto[];
  contextInfo: TextGenerationContextInfoDto;
  instructionsRequired: boolean;
  novelId: string;
}

@Component({
  selector: 'app-generate-text',
  standalone: true,
  imports: [FormsModule, ReactiveFormsModule, MatDialogActions, MatDialogClose],
  templateUrl: './generate-text.component.html',
  styleUrl: './generate-text.component.scss',
})
export class GenerateTextComponent implements OnInit {
  instructionsRequired = false;
  models: string[] = [];
  readonly generateService: GenerateService = inject(GenerateService);

  formGroup = new FormGroup({
    promptId: new FormControl('', [Validators.required]),
    model: new FormControl('', [Validators.required]),
    instructions: new FormControl(''),
  });

  constructor(
    @Inject(MAT_DIALOG_DATA)
    public data: GenerateTextComponentData,
    public dialogRef: MatDialogRef<GenerateTextComponent>
  ) {
    if (data.prompts.length === 0) {
      throw new Error('No prompts provided');
    }

    this.formGroup.patchValue({
      promptId: this.data.prompts[0].id,
    });

    if (!data.instructionsRequired) {
      this.formGroup.get('instructions')!.disable();
    } else {
      // Add the validators
      this.formGroup.get('instructions')!.setValidators([Validators.required]);
    }

    this.instructionsRequired = data.instructionsRequired;
  }

  ngOnInit(): void {
    this.getModels();
  }

  getModels() {
    this.generateService.getAvailableModels().subscribe((models) => {
      this.models = models;
      this.formGroup.patchValue({ model: models[0] });
    });
  }

  accept(): void {
    this.dialogRef.close(<GenerateTextRequestDto>{
      promptId: this.formGroup.get('promptId')!.value,
      model: this.formGroup.get('model')!.value,
      contextInfo: {
        ...this.data.contextInfo,
        instructions: this.formGroup.get('instructions')!.value,
      },
      novelId: this.data.novelId,
    });
  }

  // TODO: There is a better way to do this
  getPromptName(promptId: string): string {
    const prompt = this.data.prompts.find((p) => p.id === promptId);
    return prompt ? prompt.name : '';
  }
}
