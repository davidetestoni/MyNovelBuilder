import { ChatMessage } from './chat';

export interface UpdateChatDto {
  name: string | null;
  chapterIndex: number | null;
  compendiumIds: string[];
  compendiumRecordIds: string[];
  messages: ChatMessage[];
}
