import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environment';
import { Chat } from '../types/dtos/chats/chat';
import { ChatMetadata } from '../types/dtos/chats/chat-metadata';
import { CreateChatDto } from '../types/dtos/chats/create-chat.dto';
import { UpdateChatDto } from '../types/dtos/chats/update-chat.dto';
import { ChatService } from './chat.service';

@Injectable()
export class ApiChatService extends ChatService {
  private http = inject(HttpClient);

  private baseUrl = environment.api.baseUrl;

  getChats(): Observable<ChatMetadata[]> {
    return this.http.get<ChatMetadata[]>(`${this.baseUrl}/chats`);
  }

  getChat(chatId: string): Observable<Chat> {
    return this.http.get<Chat>(`${this.baseUrl}/chat/${chatId}`);
  }

  createChat(dto: CreateChatDto): Observable<Chat> {
    return this.http.post<Chat>(`${this.baseUrl}/chat`, dto);
  }

  updateChat(chatId: string, dto: UpdateChatDto): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/chat/${chatId}`, dto);
  }

  deleteChat(chatId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/chat/${chatId}`);
  }
}
