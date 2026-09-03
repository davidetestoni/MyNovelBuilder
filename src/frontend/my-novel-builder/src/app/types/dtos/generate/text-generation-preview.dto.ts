import { PromptMessageRole } from '../../enums/prompt-message-role';

export interface TextGenerationPreviewMessageDto {
  role: PromptMessageRole;
  message: string;
}

export interface TextGenerationPreviewDto {
  inputTokens: number;
  includedCompendiumRecordIds: string[];
  finalMessages: TextGenerationPreviewMessageDto[];
}
