import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { Chat } from '../types/dtos/chats/chat';
import { ChatMetadata } from '../types/dtos/chats/chat-metadata';
import { CreateChatDto } from '../types/dtos/chats/create-chat.dto';
import { UpdateChatDto } from '../types/dtos/chats/update-chat.dto';
import { mockedChat, mockedChats } from './mocks/mock-chat.data';
import { mockObservable } from './mocks/mock-utils';
import { ChatService } from './chat.service';

@Injectable()
export class MockChatService extends ChatService {
  getChats(): Observable<ChatMetadata[]> {
    return mockObservable(mockedChats);
  }

  getChat(_chatId: string): Observable<Chat> {
    return mockObservable(mockedChat);
  }

  createChat(_dto: CreateChatDto): Observable<Chat> {
    return mockObservable(mockedChat);
  }

  updateChat(_chatId: string, _dto: UpdateChatDto): Observable<void> {
    return mockObservable<void>(undefined);
  }

  deleteChat(_chatId: string): Observable<void> {
    return mockObservable<void>(undefined);
  }
}
