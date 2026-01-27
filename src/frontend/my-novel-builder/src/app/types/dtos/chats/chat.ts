import { ChatMessageRole } from '../../enums/chat-message-role';

export interface Chat {
  id: string;
  createdAt: string;
  updatedAt: string;
  name: string | null;
  context: ChatContext;
  messages: ChatMessage[];
}

export interface ChatContext {
  novelId: string;
  chapterIndex: number | null;
  compendiumIds: string[];
  compendiumRecordIds: string[];
}

export interface ChatMessage {
  id: string;
  sentAt: string;
  role: ChatMessageRole;
  textContent: string;
}
