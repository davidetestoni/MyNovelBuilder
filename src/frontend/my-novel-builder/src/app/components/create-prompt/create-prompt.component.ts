import { TitleCasePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import {
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ToastrModule, ToastrService } from 'ngx-toastr';
import { PromptService } from '../../services/prompt.service';
import { PromptType } from '../../types/enums/prompt-type';
import { SpacedPipe } from '../../pipes/spaced.pipe';
import { DynamicDialogRef } from 'primeng/dynamicdialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-create-prompt',
  standalone: true,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    ToastrModule,
    SpacedPipe,
    InputTextModule,
    SelectModule,
    ButtonModule,
  ],
  templateUrl: './create-prompt.component.html',
  styleUrl: './create-prompt.component.scss',
})
export class CreatePromptComponent {
  dialogRef = inject(DynamicDialogRef);
  private toastr = inject(ToastrService);

  readonly promptService: PromptService = inject(PromptService);

  formGroup = new FormGroup({
    name: new FormControl('', [Validators.required, Validators.maxLength(100)]),
    type: new FormControl(PromptType.GenerateText, [
      Validators.required,
      Validators.pattern(Object.values(PromptType).join('|')),
    ]),
  });

  promptTypes: PromptType[] = Object.values(PromptType);

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
