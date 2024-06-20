import { PromptType } from '../../enums/prompt-type';
import { PromptMessageDto } from './prompt-message.dto';

export interface PromptDto {
  createdAt: string;
  updatedAt: string;
  name: string;
  type: PromptType;
  messages: PromptMessageDto[];
}
