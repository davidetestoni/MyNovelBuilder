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
  OnInit,
} from '@angular/core';
import { RouterModule } from '@angular/router';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
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
import { PromptService } from '../../services/prompt.service';
import { TextareaModule } from 'primeng/textarea';
import { MarkdownComponent } from 'ngx-markdown';
import {
  HttpDownloadProgressEvent,
  HttpEventType,
  HttpResponse,
} from '@angular/common/http';
import {
  GenerateTextRequestDto,
  SendChatMessageContextInfoDto,
  TextGenerationType,
} from '../../types/dtos/generate/generate-text-request.dto';
import { PromptType } from '../../types/enums/prompt-type';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { v4 as uuidv4 } from 'uuid';
import { GenerateTextResponseChunkDto } from '../../types/dtos/generate/generate-text-response-chunk.dto';
import { LocalStorageService } from '../../services/local-storage.service';
import { LocalStorageKey } from '../../types/enums/local-storage-key';

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
  ],
  providers: [ConfirmationService, DialogService],
})
export class ChatComponent
  implements OnChanges, OnDestroy, AfterViewChecked, OnInit
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
  private promptService = inject(PromptService);
  private localStorageService = inject(LocalStorageService);

  private dialogRef: DynamicDialogRef | null = null;
  private shouldScrollToBottom = false;

  ChatMessageRole = ChatMessageRole;

  novel = signal<NovelDto | null>(null);
  novelNotFound = signal(false);
  prose = signal<Prose | null>(null);
  compendia = signal<CompendiumDto[] | null>(null);

  models: string[] = [];
  selectedModel: string | null = null;
  prompts: PromptDto[] = [];
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

  ngOnInit(): void {
    this.generateTextService.getAvailableModels().subscribe((models) => {
      this.models = models;
      if (models.length > 0) {
        this.selectedModel = models[0];
      }
    });

    this.promptService.getPrompts().subscribe((prompts) => {
      this.prompts = prompts.filter(
        (p) => p.type === PromptType.SendChatMessage,
      );

      const savedPromptId = this.localStorageService.getNestedStringForKey(
        LocalStorageKey.RecentPrompts,
        PromptType.SendChatMessage,
      );

      if (
        savedPromptId &&
        this.prompts.some((p) => p.id === savedPromptId)
      ) {
        this.selectedPromptId = savedPromptId;
      } else if (this.prompts.length > 0) {
        this.selectedPromptId = this.prompts[0].id;
      }
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['currentChat'] && this.currentChat) {
      this.loadNovelContext();
      this.shouldScrollToBottom = true;
    }
  }

  ngAfterViewChecked(): void {
    if (this.shouldScrollToBottom) {
      this.scrollToBottom();
    }
  }

  sendMessage() {
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
    if (this.dialogRef) {
      this.dialogRef.close();
    }
  }

  loadNovelContext(): void {
    const novelId = this.currentChat.context.novelId;
    this.novelNotFound.set(false);

    this.novelService.getNovel(novelId).subscribe({
      next: (novel) => {
        this.novel.set(novel);
        this.loadCompendia(novel);
      },
      error: () => {
        this.novelNotFound.set(true);
      },
    });

    this.novelService.getNovelProse(novelId).subscribe({
      next: (prose) => {
        this.prose.set(prose);
      },
      error: () => {
        // Handle error if needed
      },
    });
  }

  loadCompendia(novel: NovelDto): void {
    this.compendiumService.getCompendia().subscribe((compendia) => {
      this.compendia.set(
        compendia.filter((compendium) =>
          novel.compendiumIds.includes(compendium.id),
        ),
      );
    });
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
    navigator.clipboard.writeText(textContent).then(() => {
      this.toastr.success('Message copied to clipboard');
    });
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

  resendMessage(message: ChatMessage): void {
    if (!this.canResend(message)) {
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

    const contextInfo: SendChatMessageContextInfoDto = {
      $type: TextGenerationType.SendChatMessage,
      chapterIndex: this.currentChat.context.chapterIndex,
      userMessage: userMessageContent,
      compendiumIds: this.currentChat.context.compendiumIds,
      compendiumRecordIds: this.currentChat.context.compendiumRecordIds,
    };

    const request: GenerateTextRequestDto = {
      model: this.selectedModel,
      promptId: this.selectedPromptId,
      novelId: this.currentChat.context.novelId,
      contextInfo: contextInfo,
    };

    this.generateTextService.generateText(request).subscribe({
      next: (event) => {
        if (event.type === HttpEventType.DownloadProgress) {
          const response = (event as HttpDownloadProgressEvent)
            .partialText as string;
          if (response === undefined) {
            return;
          }

          const responseChunks = response
            .split('\n')
            .filter((item) => item.length > 0)
            .map((item) => JSON.parse(item) as GenerateTextResponseChunkDto);

          if (responseChunks.length > 0) {
            const message = responseChunks.map((item) => item.content).join('');
            assistantMessage.textContent = message;
            this.shouldScrollToBottom = true;
          }
        } else if (event.type === HttpEventType.Response) {
          const response = event as HttpResponse<string>;
          const responseChunks = response
            .body!.split('\n')
            .filter((item) => item.length > 0)
            .map((item) => JSON.parse(item) as GenerateTextResponseChunkDto);

          if (responseChunks.length > 0) {
            const message = responseChunks.map((item) => item.content).join('');
            assistantMessage.textContent = message;
            this.isGenerating = false;
            this.saveChat();
            this.shouldScrollToBottom = true;
          }
        }
      },
      error: (err) => {
        console.error('Error generating text:', err);
        this.toastr.error('Failed to generate response');
        this.isGenerating = false;
        // Remove the empty assistant message if it failed
        if (!assistantMessage.textContent) {
          this.currentChat.messages = this.currentChat.messages.filter(
            (m) => m.id !== assistantMessage.id,
          );
        }
      },
    });
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
