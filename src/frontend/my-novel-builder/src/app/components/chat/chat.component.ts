import {
  Component,
  Input,
  inject,
  OnChanges,
  SimpleChanges,
  signal,
  computed,
  OnDestroy,
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
  ],
  providers: [ConfirmationService, DialogService],
})
export class ChatComponent implements OnChanges, OnDestroy {
  @Input() currentChatId!: string;
  @Input() currentChat!: Chat;

  readonly chatService = inject(ChatService);
  readonly novelService = inject(NovelService);
  readonly compendiumService = inject(CompendiumService);
  readonly confirmationService = inject(ConfirmationService);
  readonly toastr = inject(ToastrService);
  private dialogService = inject(DialogService);
  private dialogRef: DynamicDialogRef | null = null;

  ChatMessageRole = ChatMessageRole;

  novel = signal<NovelDto | null>(null);
  prose = signal<Prose | null>(null);
  compendia = signal<CompendiumDto[] | null>(null);

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
      this.loadNovelContext();
    }
  }

  ngOnDestroy(): void {
    if (this.dialogRef) {
      this.dialogRef.close();
    }
  }

  loadNovelContext(): void {
    const novelId = this.currentChat.context.novelId;
    this.novelService.getNovel(novelId).subscribe((novel) => {
      this.novel.set(novel);
      this.loadCompendia(novel);
    });
    this.novelService.getNovelProse(novelId).subscribe((prose) => {
      this.prose.set(prose);
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

  private saveChat(): void {
    const dto: UpdateChatDto = {
      name: this.currentChat.name,
      chapterIndex: this.currentChat.context.chapterIndex,
      compendiumIds: this.currentChat.context.compendiumIds,
      compendiumRecordIds: this.currentChat.context.compendiumRecordIds,
      messages: this.currentChat.messages,
    };
    this.chatService.updateChat(this.currentChatId, dto).subscribe();
  }
}
