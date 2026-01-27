import { inject, Injectable } from '@angular/core';
import { environment } from '../../environment';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ChatMetadata } from '../types/dtos/chats/chat-metadata';
import { indexToGuid, mockedChat, mockedChats, mockObservable } from './mock';
import { Chat } from '../types/dtos/chats/chat';
import { CreateChatDto } from '../types/dtos/chats/create-chat.dto';
import { UpdateChatDto } from '../types/dtos/chats/update-chat.dto';

@Injectable({
  providedIn: 'root',
})
export class ChatService {
  private http = inject(HttpClient);

  private baseUrl = environment.api.baseUrl;
  private mocked = environment.mocked;

  getChats(): Observable<ChatMetadata[]> {
    return this.mocked
      ? mockObservable(mockedChats)
      : this.http.get<ChatMetadata[]>(`${this.baseUrl}/chats`);
  }

  getChat(chatId: string): Observable<Chat> {
    return this.mocked
      ? mockObservable(mockedChat)
      : this.http.get<Chat>(`${this.baseUrl}/chat/${chatId}`);
  }

  createChat(dto: CreateChatDto): Observable<Chat> {
    return this.mocked
      ? mockObservable(mockedChat)
      : this.http.post<Chat>(`${this.baseUrl}/chat`, dto);
  }

  updateChat(chatId: string, dto: UpdateChatDto): Observable<void> {
    return this.mocked
      ? mockObservable<void>(undefined)
      : this.http.put<void>(`${this.baseUrl}/chat/${chatId}`, dto);
  }

  deleteChat(chatId: string): Observable<void> {
    return this.mocked
      ? mockObservable<void>(undefined)
      : this.http.delete<void>(`${this.baseUrl}/chat/${chatId}`);
  }
}
