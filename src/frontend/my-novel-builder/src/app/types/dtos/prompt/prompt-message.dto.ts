import { PromptMessageRole } from '../../enums/prompt-message-role';

export interface PromptMessageDto {
  id: number;
  role: PromptMessageRole;
  message: string;
}
