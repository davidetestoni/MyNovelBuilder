import { ChatMessageRole } from '../../enums/chat-message-role';

export interface NovelGenerateTextRequestDto {
  model: string;
  promptId: string;
  novelId: string;
  contextInfo: NovelTextGenerationContextInfoDto;
}

export interface NovelTextGenerationContextInfoDto {
  $type: NovelTextGenerationType;
}

export interface GenerateTextContextInfoDto extends NovelTextGenerationContextInfoDto {
  $type: NovelTextGenerationType.GenerateText;
  chapterIndex: number;
  sectionIndex: number;
  textOffset: number;
  instructions: string | null;
}

export interface SummarizeTextContextInfoDto extends NovelTextGenerationContextInfoDto {
  $type: NovelTextGenerationType.SummarizeText;
  chapterIndex: number;
  sectionIndex: number;
}

export interface ReplaceTextContextInfoDto extends NovelTextGenerationContextInfoDto {
  $type: NovelTextGenerationType.ReplaceText;
  chapterIndex: number;
  sectionIndex: number;
  textOffset: number;
  textLength: number;
  instructions: string | null;
}

export interface CreateCompendiumRecordContextInfoDto extends NovelTextGenerationContextInfoDto {
  $type: NovelTextGenerationType.CreateCompendiumRecord;
  chapterIndex: number;
  sectionIndex: number;
  textOffset: number;
  textLength: number;
  instructions: string | null;
}

export interface EditCompendiumRecordContextInfoDto extends NovelTextGenerationContextInfoDto {
  $type: NovelTextGenerationType.EditCompendiumRecord;
  chapterIndex: number;
  sectionIndex: number;
  textOffset: number;
  textLength: number;
  recordId: string;
  instructions: string | null;
}

export interface ChatMessageDto {
  role: ChatMessageRole;
  textContent: string;
}

export interface SendChatMessageContextInfoDto extends NovelTextGenerationContextInfoDto {
  $type: NovelTextGenerationType.SendChatMessage;
  chapterIndex: number | null;
  userMessage: string;
  previousMessages: ChatMessageDto[];
  compendiumIds: string[];
  compendiumRecordIds: string[];
}

export enum NovelTextGenerationType {
  GenerateText = 'generateText',
  SummarizeText = 'summarizeText',
  ReplaceText = 'replaceText',
  CreateCompendiumRecord = 'createCompendiumRecord',
  EditCompendiumRecord = 'editCompendiumRecord',
  SendChatMessage = 'sendChatMessage',
}
