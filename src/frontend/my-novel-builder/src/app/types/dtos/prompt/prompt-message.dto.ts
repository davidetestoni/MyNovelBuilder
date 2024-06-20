import { PromptMessageRole } from '../../enums/prompt-message-role';

export interface PromptMessageDto {
  role: PromptMessageRole;
  message: string;
}
