import { TitleCasePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import {
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import {
  MatDialogActions,
  MatDialogClose,
  MatDialogRef,
} from '@angular/material/dialog';
import { ToastrModule, ToastrService } from 'ngx-toastr';
import { PromptService } from '../../services/prompt.service';
import { PromptType } from '../../types/enums/prompt-type';
import { SpacedPipe } from '../../pipes/spaced.pipe';

@Component({
  selector: 'app-create-prompt',
  standalone: true,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    MatDialogActions,
    MatDialogClose,
    ToastrModule,
    SpacedPipe,
  ],
  templateUrl: './create-prompt.component.html',
  styleUrl: './create-prompt.component.scss',
})
export class CreatePromptComponent {
  readonly promptService: PromptService = inject(PromptService);

  formGroup = new FormGroup({
    name: new FormControl('', [Validators.required, Validators.maxLength(100)]),
    type: new FormControl(PromptType.GenerateText, [
      Validators.required,
      Validators.pattern(Object.values(PromptType).join('|')),
    ]),
  });

  promptTypes: PromptType[] = [
    PromptType.GenerateText,
    PromptType.SummarizeText,
    PromptType.ReplaceText,
    PromptType.CreateCompendiumRecord,
    PromptType.EditCompendiumRecord,
  ];

  constructor(
    public dialogRef: MatDialogRef<CreatePromptComponent>,
    private toastr: ToastrService
  ) {}

  createPrompt(): void {
    this.promptService
      .createPrompt({
        name: this.formGroup.get('name')!.value!,
        type: this.formGroup.get('type')!.value!,
        messages: [],
      })
      .subscribe((prompt) => {
        this.toastr.success('Prompt created successfully!');
        this.dialogRef.close(prompt);
      });
  }
}
