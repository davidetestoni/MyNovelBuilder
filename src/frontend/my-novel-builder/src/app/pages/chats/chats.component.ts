import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import moment from 'moment';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { ReactiveFormsModule } from '@angular/forms';
import { ChatMetadata } from '../../types/dtos/chats/chat-metadata';
import { ChatService } from '../../services/chat.service';
import { Tooltip } from 'primeng/tooltip';
import { Chat } from '../../types/dtos/chats/chat';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ChatComponent } from '../../components/chat/chat.component';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { CreateChatComponent } from '../../components/create-chat/create-chat.component';

@Component({
  selector: 'app-chats',
  standalone: true,
  templateUrl: './chats.component.html',
  styleUrls: ['./chats.component.scss'],
  imports: [
    RouterModule,
    ReactiveFormsModule,
    Tooltip,
    ConfirmDialogModule,
    ChatComponent,
  ],
  providers: [ConfirmationService, DialogService],
})
export class ChatsComponent implements OnInit, OnDestroy {
  chats: ChatMetadata[] | null = null;
  currentChatId: string | null = null;
  currentChat: Chat | null = null;
  private dialogService = inject(DialogService);
  private dialogRef: DynamicDialogRef | null = null;
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  readonly chatService = inject(ChatService);
  readonly confirmationService = inject(ConfirmationService);

  ngOnInit(): void {
    this.getChats();
    this.route.paramMap.subscribe((params) => {
      const id = params.get('id');
      if (id) {
        this.loadChat(id);
      } else {
        this.currentChatId = null;
        this.currentChat = null;
      }
    });
  }

  ngOnDestroy(): void {
    if (this.dialogRef) {
      this.dialogRef.close();
    }
  }

  getChats(): void {
    this.chatService.getChats().subscribe((chats) => {
      this.chats = chats;
    });
  }

  loadChat(chatId: string): void {
    if (this.currentChatId !== chatId) {
      this.chatService.getChat(chatId).subscribe((chat) => {
        this.currentChat = chat;
        this.currentChatId = chatId;
      });
    }
  }

  selectChat(chatId: string): void {
    this.router.navigate(['/chat', chatId]);
  }

  deleteChat(chatId: string): void {
    this.confirmationService.confirm({
      message: 'Are you sure you want to delete this chat?',
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.chatService.deleteChat(chatId).subscribe(() => {
          this.chats = this.chats?.filter((chat) => chat.id !== chatId) || null;
          if (this.currentChatId === chatId) {
            this.router.navigate(['/chat']);
          }
        });
      },
    });
  }

  openCreateChatDialog(): void {
    this.dialogRef = this.dialogService.open(CreateChatComponent, {
      header: 'Create a chat',
      width: '400px',
      modal: true,
      closable: true,
      closeOnEscape: true,
      dismissableMask: true,
    });

    this.dialogRef?.onClose.subscribe((result: Chat | undefined) => {
      if (result) {
        const metadata: ChatMetadata = {
          id: result.id,
          name: result.name,
          createdAt: result.createdAt,
          updatedAt: result.updatedAt,
        };
        this.chats = [metadata, ...(this.chats || [])];
        this.router.navigate(['/chat', result.id]);
      }
    });
  }

  getLastUpdated(chat: ChatMetadata): string {
    return moment(chat.updatedAt).fromNow();
  }
}
