import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SpacedPipe } from '../../pipes/spaced.pipe';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { PromptService } from '../../services/prompt.service';
import { PromptType } from '../../types/enums/prompt-type';
import { PromptMessageRole } from '../../types/enums/prompt-message-role';
import { TitleCasePipe } from '@angular/common';
import { PromptMessageDto } from '../../types/dtos/prompt/prompt-message.dto';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { TextareaModule } from 'primeng/textarea';

@Component({
  selector: 'app-prompt',
  standalone: true,
  imports: [
    FormsModule,
    TitleCasePipe,
    SpacedPipe,
    InputTextModule,
    TextareaModule,
    ButtonModule,
    ConfirmDialogModule,
  ],
  providers: [ConfirmationService],
  templateUrl: './prompt.component.html',
  styleUrl: './prompt.component.scss',
})
export class PromptComponent {
  @Input() prompt!: PromptDto;
  @Output() updatePrompt = new EventEmitter<PromptDto>();
  @Output() deletePrompt = new EventEmitter<PromptDto>();
  promptService: PromptService = inject(PromptService);
  private confirmationService = inject(ConfirmationService);

  promptTypes: PromptType[] = [
    PromptType.GenerateText,
    PromptType.SummarizeText,
    PromptType.ReplaceText,
    PromptType.CreateCompendiumRecord,
    PromptType.EditCompendiumRecord,
  ];

  promptMessageRoles: PromptMessageRole[] = [
    PromptMessageRole.System,
    PromptMessageRole.User,
    PromptMessageRole.Assistant,
  ];

  PromptType = PromptType;
  PromptMessageRole = PromptMessageRole;

  onBlur(): void {
    this.updatePrompt.emit(this.prompt);
  }

  addMessage(role: PromptMessageRole): void {
    this.prompt.messages = [
      ...this.prompt.messages,
      {
        id: this.prompt.messages.length,
        role,
        message: '',
      },
    ];
    this.onBlur();
  }

  removeMessage(message: PromptMessageDto): void {
    this.prompt.messages = this.prompt.messages.filter((m) => m !== message);
    this.onBlur();
  }

  removePrompt(): void {
    this.confirmationService.confirm({
      message:
        'Are you sure you want to delete this prompt? You cannot undo this action.',
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.deletePrompt.emit(this.prompt);
      },
    });
  }
}
