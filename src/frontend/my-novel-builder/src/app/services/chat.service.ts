import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ChatMetadata } from '../types/dtos/chats/chat-metadata';
import { Chat } from '../types/dtos/chats/chat';
import { CreateChatDto } from '../types/dtos/chats/create-chat.dto';
import { UpdateChatDto } from '../types/dtos/chats/update-chat.dto';

@Injectable()
export abstract class ChatService {
  abstract getChats(): Observable<ChatMetadata[]>;
  abstract getChat(chatId: string): Observable<Chat>;
  abstract createChat(dto: CreateChatDto): Observable<Chat>;
  abstract updateChat(chatId: string, dto: UpdateChatDto): Observable<void>;
  abstract deleteChat(chatId: string): Observable<void>;
}
