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
import { CodeEditorComponent } from '../code-editor/code-editor.component';

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
    CodeEditorComponent,
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
    PromptType.SendChatMessage,
  ];

  promptMessageRoles: PromptMessageRole[] = [
    PromptMessageRole.System,
    PromptMessageRole.User,
    PromptMessageRole.Assistant,
  ];

  PromptType = PromptType;
  PromptMessageRole = PromptMessageRole;

  keywordsByPromptType: Record<
    string,
    { keyword: string; description: string }[]
  > = {
    [PromptType.GenerateText]: [
      {
        keyword: '{{context}}',
        description: 'The story content preceding the current position.',
      },
      {
        keyword: '{{instructions}}',
        description: 'The specific instructions for this generation.',
      },
      {
        keyword: '{{records}}',
        description: 'Information from relevant compendium records.',
      },
    ],
    [PromptType.SummarizeText]: [
      {
        keyword: '{{context}}',
        description: 'The text of the section to be summarized.',
      },
      {
        keyword: '{{records}}',
        description: 'Information from relevant compendium records.',
      },
    ],
    [PromptType.ReplaceText]: [
      {
        keyword: '{{textBefore}}',
        description: 'The story content before the selected text.',
      },
      {
        keyword: '{{textAfter}}',
        description: 'The story content after the selected text.',
      },
      {
        keyword: '{{instructions}}',
        description: 'The specific instructions for the replacement.',
      },
      {
        keyword: '{{textToReplace}}',
        description: 'The actual text that is being replaced.',
      },
      {
        keyword: '{{records}}',
        description: 'Information from relevant compendium records.',
      },
    ],
    [PromptType.CreateCompendiumRecord]: [
      {
        keyword: '{{context}}',
        description: 'The story content preceding the selection.',
      },
      {
        keyword: '{{instructions}}',
        description: 'User instructions for creating the record.',
      },
      {
        keyword: '{{recordDetails}}',
        description:
          'The selected text used to extract record information.',
      },
      {
        keyword: '{{records}}',
        description: 'Information from relevant compendium records.',
      },
    ],
    [PromptType.EditCompendiumRecord]: [
      {
        keyword: '{{context}}',
        description: 'The story content preceding the selection.',
      },
      {
        keyword: '{{instructions}}',
        description: 'User instructions for editing the record.',
      },
      {
        keyword: '{{recordDetails}}',
        description:
          'The selected text used to extract record information.',
      },
      {
        keyword: '{{records}}',
        description: 'Information from relevant compendium records.',
      },
    ],
    [PromptType.SendChatMessage]: [
      {
        keyword: '{{context}}',
        description:
          'The story content (chapter or entire novel) relevant to the chat.',
      },
      {
        keyword: '{{chatHistory}}',
        description: 'The previous messages in the current chat session.',
      },
      {
        keyword: '{{instructions}}',
        description: "The user's latest message in the chat.",
      },
      {
        keyword: '{{records}}',
        description: 'Information from selected compendium records.',
      },
    ],
  };

  commonKeywords = [
    {
      keyword: '{{novel.language}}',
      description: 'The writing language set for the novel.',
    },
    {
      keyword: '{{novel.pov}}',
      description: 'The point of view and perspective of the novel.',
    },
    {
      keyword: '{{novel.tense}}',
      description: 'The writing tense of the novel.',
    },
  ];

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
