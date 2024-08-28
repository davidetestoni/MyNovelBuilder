import { Component, Inject } from '@angular/core';
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
import { GenerateTextRequestDto } from '../../types/dtos/generate/generate-text-request.dto';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';

export interface GenerateTextComponentData {
  prompts: PromptDto[];
  instructions: string | null;
  context: string | null;
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
export class GenerateTextComponent {
  prompts: PromptDto[] = [];
  models: string[] = ['undi95/toppy-m-7b:free'];
  instructionsRequired = false;

  formGroup = new FormGroup({
    promptId: new FormControl('', [Validators.required]),
    model: new FormControl('', [Validators.required]),
    instructions: new FormControl(''),
    context: new FormControl(''),
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
      instructions: data.instructions,
      context: data.context,
      model: this.models[0],
    });

    if (!data.instructionsRequired) {
      this.formGroup.get('instructions')!.disable();
    } else {
      // Add the validators
      this.formGroup.get('instructions')!.setValidators([Validators.required]);
    }

    this.prompts = this.data.prompts;
    this.instructionsRequired = data.instructionsRequired;
  }

  accept(): void {
    this.dialogRef.close(<GenerateTextRequestDto>{
      promptId: this.formGroup.get('promptId')!.value,
      model: this.formGroup.get('model')!.value,
      instructions: this.formGroup.get('instructions')!.value,
      context: this.formGroup.get('context')!.value,
      novelId: this.data.novelId,
    });
  }

  // TODO: There is a better way to do this
  getPromptName(promptId: string): string {
    const prompt = this.prompts.find((p) => p.id === promptId);
    return prompt ? prompt.name : '';
  }
}
