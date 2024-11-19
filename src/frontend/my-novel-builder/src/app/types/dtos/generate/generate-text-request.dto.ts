import { CompendiumRecordType } from '../../enums/compendium-record-type';

export interface GenerateTextRequestDto {
  model: string;
  promptId: string;
  novelId: string;
  contextInfo: TextGenerationContextInfoDto;
}

export interface TextGenerationContextInfoDto {
  $type: TextGenerationType;
}

export interface GenerateTextContextInfoDto
  extends TextGenerationContextInfoDto {
  $type: TextGenerationType.GenerateText;
  chapterIndex: number;
  sectionIndex: number;
  textOffset: number;
  instructions: string | null;
}

export interface SummarizeTextContextInfoDto
  extends TextGenerationContextInfoDto {
  $type: TextGenerationType.SummarizeText;
  chapterIndex: number;
  sectionIndex: number;
}

export interface ReplaceTextContextInfoDto
  extends TextGenerationContextInfoDto {
  $type: TextGenerationType.ReplaceText;
  chapterIndex: number;
  sectionIndex: number;
  textOffset: number;
  textLength: number;
  instructions: string | null;
}

export interface CreateCompendiumRecordContextInfoDto
  extends TextGenerationContextInfoDto {
  $type: TextGenerationType.CreateCompendiumRecord;
  chapterIndex: number;
  sectionIndex: number;
  textOffset: number;
  textLength: number;
  instructions: string | null;
}

export interface EditCompendiumRecordContextInfoDto
  extends TextGenerationContextInfoDto {
  $type: TextGenerationType.EditCompendiumRecord;
  chapterIndex: number;
  sectionIndex: number;
  textOffset: number;
  textLength: number;
  recordId: string;
  instructions: string | null;
}

export enum TextGenerationType {
  GenerateText = 'generateText',
  SummarizeText = 'summarizeText',
  ReplaceText = 'replaceText',
  CreateCompendiumRecord = 'createCompendiumRecord',
  EditCompendiumRecord = 'editCompendiumRecord',
}
