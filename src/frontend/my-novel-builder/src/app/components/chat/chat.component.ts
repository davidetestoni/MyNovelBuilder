import {
  Component,
  Input,
  Output,
  EventEmitter,
  inject,
  OnChanges,
  SimpleChanges,
  signal,
  computed,
  OnDestroy,
  ViewChild,
  ElementRef,
  AfterViewChecked,
} from '@angular/core';
import { RouterModule } from '@angular/router';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { ChatService } from '../../services/chat.service';
import { Chat, ChatMessage } from '../../types/dtos/chats/chat';
import { NovelService } from '../../services/novel.service';
import { NovelDto } from '../../types/dtos/novel/novel.dto';
import { Prose } from '../../types/dtos/novel/prose';
import { Select } from 'primeng/select';
import { MultiSelect } from 'primeng/multiselect';
import { InputTextModule } from 'primeng/inputtext';
import { UpdateChatDto } from '../../types/dtos/chats/update-chat.dto';
import { CompendiumService } from '../../services/compendium.service';
import { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import { ChatMessageRole } from '../../types/enums/chat-message-role';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastrService } from 'ngx-toastr';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { EditChatMessageComponent } from '../edit-chat-message/edit-chat-message.component';
import { GenerateTextService } from '../../services/generate-text.service';
import { TextareaModule } from 'primeng/textarea';
import { MarkdownComponent } from 'ngx-markdown';
import {
  GenerateTextRequestDto,
  SendChatMessageContextInfoDto,
  NovelTextGenerationType,
  ChatMessageDto,
} from '../../types/dtos/generate/generate-text-request.dto';
import { PromptType } from '../../types/enums/prompt-type';
import { v4 as uuidv4 } from 'uuid';
import { LocalStorageService } from '../../services/local-storage.service';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { PromptSelectComponent } from '../prompt-select/prompt-select.component';
import { ModelSelectComponent } from '../model-select/model-select.component';
import { CompendiumOptionPreviewComponent } from '../compendium-option-preview/compendium-option-preview.component';
import { RecordOptionPreviewComponent } from '../record-option-preview/record-option-preview.component';

@Component({
  selector: 'app-chat',
  standalone: true,
  templateUrl: './chat.component.html',
  styleUrls: ['./chat.component.scss'],
  imports: [
    RouterModule,
    ReactiveFormsModule,
    FormsModule,
    Select,
    MultiSelect,
    InputTextModule,
    ConfirmDialogModule,
    TextareaModule,
    MarkdownComponent,
    PromptSelectComponent,
    ModelSelectComponent,
    CompendiumOptionPreviewComponent,
    RecordOptionPreviewComponent,
  ],
  providers: [ConfirmationService, DialogService],
})
export class ChatComponent
  implements OnChanges, OnDestroy, AfterViewChecked
{
  @Input() currentChatId!: string;
  @Input() currentChat!: Chat;
  @Output() onChatUpdated = new EventEmitter<void>();
  @ViewChild('chatContainer') private chatContainer!: ElementRef;

  readonly chatService = inject(ChatService);
  readonly novelService = inject(NovelService);
  readonly compendiumService = inject(CompendiumService);
  readonly confirmationService = inject(ConfirmationService);
  readonly toastr = inject(ToastrService);
  private dialogService = inject(DialogService);
  private generateTextService = inject(GenerateTextService);
  private localStorageService = inject(LocalStorageService);

  private dialogRef: DynamicDialogRef | null = null;
  private contextSubscriptions = new Subscription();
  private generationSubscription: Subscription | null = null;
  private shouldScrollToBottom = false;

  ChatMessageRole = ChatMessageRole;
  PromptType = PromptType;

  novel = signal<NovelDto | null>(null);
  novelNotFound = signal(false);
  prose = signal<Prose | null>(null);
  compendia = signal<CompendiumDto[] | null>(null);

  selectedModel: string | null = null;
  promptCount = -1;
  selectedPromptId: string | null = null;

  userInput = '';
  isGenerating = false;

  chapters = computed(() => {
    const prose = this.prose();
    if (!prose) {
      return [];
    }
    return prose.chapters.map((chapter, index) => ({
      label: chapter.title,
      value: index,
    }));
  });

  allAvailableRecords = computed(() => {
    return this.compendia()?.flatMap((c) => c.records) ?? [];
  });

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['currentChat'] && this.currentChat) {
      this.cancelGeneration();
      this.loadNovelContext();
      this.shouldScrollToBottom = true;
    }
  }

  ngAfterViewChecked(): void {
    if (this.shouldScrollToBottom) {
      this.scrollToBottom();
    }
  }

  sendMessage(): void {
    if (
      !this.userInput.trim() ||
      this.isGenerating ||
      !this.selectedModel ||
      !this.selectedPromptId
    ) {
      return;
    }

    const userMessageContent = this.userInput;
    this.userInput = '';

    // Add user message
    const userMessage: ChatMessage = {
      id: uuidv4(),
      sentAt: new Date().toISOString(),
      role: ChatMessageRole.User,
      textContent: userMessageContent,
    };
    this.currentChat.messages.push(userMessage);

    // Create placeholder assistant message
    const assistantMessage: ChatMessage = {
      id: uuidv4(),
      sentAt: new Date().toISOString(),
      role: ChatMessageRole.Assistant,
      textContent: '',
    };
    this.currentChat.messages.push(assistantMessage);

    this.executeGeneration(userMessageContent, assistantMessage);
  }

  private scrollToBottom(): void {
    try {
      this.chatContainer.nativeElement.scrollTop =
        this.chatContainer.nativeElement.scrollHeight;
      this.shouldScrollToBottom = false;
    } catch (err) {
      console.error('Error scrolling to bottom:', err);
    }
  }

  ngOnDestroy(): void {
    this.cancelGeneration();
    this.contextSubscriptions.unsubscribe();
    if (this.dialogRef) {
      this.dialogRef.close();
    }
  }

  loadNovelContext(): void {
    const novelId = this.currentChat.context.novelId;
    this.novelNotFound.set(false);
    this.novel.set(null);
    this.prose.set(null);
    this.compendia.set(null);
    this.contextSubscriptions.unsubscribe();
    this.contextSubscriptions = new Subscription();

    this.contextSubscriptions.add(
      this.novelService.getNovel(novelId).subscribe({
        next: (novel) => {
          this.novel.set(novel);
          this.loadCompendia(novel);
        },
        error: () => {
          this.novelNotFound.set(true);
        },
      }),
    );

    this.contextSubscriptions.add(
      this.novelService.getNovelProse(novelId).subscribe({
        next: (prose) => {
          this.prose.set(prose);
        },
        error: () => undefined,
      }),
    );
  }

  loadCompendia(novel: NovelDto): void {
    this.contextSubscriptions.add(
      this.compendiumService.getCompendia().subscribe({
        next: (compendia) => {
          this.compendia.set(
            compendia.filter((compendium) =>
              novel.compendiumIds.includes(compendium.id),
            ),
          );
        },
        error: () => undefined,
      }),
    );
  }

  updateChatName(name: string): void {
    this.currentChat.name = name;
    this.saveChat();
  }

  updateChatChapter(chapterIndex: number | null): void {
    this.currentChat.context.chapterIndex = chapterIndex;
    this.saveChat();
  }

  onCompendiaChange(event: any): void {
    const selectedIds = event.value as string[];
    const previousIds = this.currentChat.context.compendiumIds;

    const added = selectedIds.filter((id) => !previousIds.includes(id));

    let recordIds = [...this.currentChat.context.compendiumRecordIds];

    added.forEach((compId) => {
      const comp = this.compendia()?.find((c) => c.id === compId);
      if (comp) {
        comp.records.forEach((r) => {
          if (!recordIds.includes(r.id)) {
            recordIds.push(r.id);
          }
        });
      }
    });

    this.currentChat.context.compendiumIds = selectedIds;
    this.currentChat.context.compendiumRecordIds = recordIds;
    this.saveChat();
  }

  onRecordsChange(event: any): void {
    const selectedRecordIds = event.value as string[];
    this.currentChat.context.compendiumRecordIds = selectedRecordIds;

    let compIds = [...this.currentChat.context.compendiumIds];

    this.compendia()?.forEach((comp) => {
      const allRecordsSelected =
        comp.records.length > 0 &&
        comp.records.every((r) => selectedRecordIds.includes(r.id));

      const isCompSelected = compIds.includes(comp.id);

      if (allRecordsSelected && !isCompSelected) {
        compIds.push(comp.id);
      } else if (
        !allRecordsSelected &&
        isCompSelected &&
        comp.records.length > 0
      ) {
        compIds = compIds.filter((id) => id !== comp.id);
      }
    });

    this.currentChat.context.compendiumIds = compIds;
    this.saveChat();
  }

  editMessage(message: ChatMessage): void {
    this.dialogRef = this.dialogService.open(EditChatMessageComponent, {
      header: 'Edit Message',
      width: '50vw',
      data: {
        text: message.textContent,
      },
      modal: true,
      closable: true,
      dismissableMask: true,
    });

    this.dialogRef?.onClose.subscribe((newText: string | undefined) => {
      if (newText !== undefined && newText !== message.textContent) {
        message.textContent = newText;
        this.saveChat();
      }
    });
  }

  deleteMessage(messageId: string): void {
    this.confirmationService.confirm({
      message: 'Are you sure you want to delete this message?',
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.currentChat.messages = this.currentChat.messages.filter(
          (m) => m.id !== messageId,
        );
        this.saveChat();
      },
    });
  }

  copyMessage(textContent: string): void {
    navigator.clipboard.writeText(textContent).then(
      () => {
        this.toastr.success('Message copied to clipboard');
      },
      () => {
        this.toastr.error('Could not copy message to clipboard');
      },
    );
  }

  canResend(message: ChatMessage): boolean {
    if (message.role !== ChatMessageRole.User || this.isGenerating) {
      return false;
    }

    const index = this.currentChat.messages.indexOf(message);
    const isLastMessage = index === this.currentChat.messages.length - 1;
    const nextMessage = this.currentChat.messages[index + 1];

    return (
      isLastMessage ||
      (nextMessage && nextMessage.role !== ChatMessageRole.Assistant)
    );
  }

  onPromptOptionsChanged(count: number): void {
    this.promptCount = count;
  }

  resendMessage(message: ChatMessage): void {
    if (
      !this.canResend(message) ||
      !this.selectedModel ||
      !this.selectedPromptId
    ) {
      return;
    }

    const userMessageContent = message.textContent;
    const index = this.currentChat.messages.indexOf(message);

    // Create placeholder assistant message
    const assistantMessage: ChatMessage = {
      id: uuidv4(),
      sentAt: new Date().toISOString(),
      role: ChatMessageRole.Assistant,
      textContent: '',
    };

    // Insert after the user message
    this.currentChat.messages.splice(index + 1, 0, assistantMessage);

    this.executeGeneration(userMessageContent, assistantMessage);
  }

  private executeGeneration(
    userMessageContent: string,
    assistantMessage: ChatMessage,
  ): void {
    if (!this.selectedModel || !this.selectedPromptId) {
      return;
    }

    // Save selected prompt
    this.localStorageService.setNestedStringForKey(
      LocalStorageKey.RecentPrompts,
      PromptType.SendChatMessage,
      this.selectedPromptId,
    );

    this.isGenerating = true;
    this.shouldScrollToBottom = true;

    const assistantIndex = this.currentChat.messages.indexOf(assistantMessage);
    const previousMessages: ChatMessageDto[] = this.currentChat.messages
      .slice(0, assistantIndex - 1)
      .map((m) => ({ role: m.role, textContent: m.textContent }));

    const contextInfo: SendChatMessageContextInfoDto = {
      $type: NovelTextGenerationType.SendChatMessage,
      novelId: this.currentChat.context.novelId,
      chapterIndex: this.currentChat.context.chapterIndex,
      userMessage: userMessageContent,
      previousMessages,
      compendiumIds: this.currentChat.context.compendiumIds,
      compendiumRecordIds: this.currentChat.context.compendiumRecordIds,
    };

    const request: GenerateTextRequestDto = {
      model: this.selectedModel,
      promptId: this.selectedPromptId,
      contextInfo: contextInfo,
    };

    this.generationSubscription?.unsubscribe();
    const generationSubscription = this.generateTextService
      .generateText(request)
      .subscribe({
        next: (update) => {
          if (update.content.length > 0) {
            assistantMessage.textContent = update.content;
            this.shouldScrollToBottom = true;
          }

          if (update.isComplete) {
            this.finishGeneration();
          }
        },
        error: (err) => {
          console.error('Error generating text:', err);
          this.generationSubscription = null;
          this.isGenerating = false;
          // Remove the empty assistant message if it failed
          if (!assistantMessage.textContent) {
            this.currentChat.messages = this.currentChat.messages.filter(
              (m) => m.id !== assistantMessage.id,
            );
          }
          this.saveChat();
        },
        complete: () => {
          if (this.isGenerating) {
            this.finishGeneration();
          }
        },
      });
    this.generationSubscription = generationSubscription.closed
      ? null
      : generationSubscription;
  }

  private finishGeneration(): void {
    this.generationSubscription?.unsubscribe();
    this.generationSubscription = null;
    this.isGenerating = false;
    this.saveChat();
    this.shouldScrollToBottom = true;
  }

  private cancelGeneration(): void {
    this.generationSubscription?.unsubscribe();
    this.generationSubscription = null;
    this.isGenerating = false;
  }

  private saveChat(): void {
    const dto: UpdateChatDto = {
      name: this.currentChat.name,
      chapterIndex: this.currentChat.context.chapterIndex,
      compendiumIds: this.currentChat.context.compendiumIds,
      compendiumRecordIds: this.currentChat.context.compendiumRecordIds,
      messages: this.currentChat.messages,
    };
    this.chatService.updateChat(this.currentChatId, dto).subscribe();
    this.onChatUpdated.emit();
  }
}
