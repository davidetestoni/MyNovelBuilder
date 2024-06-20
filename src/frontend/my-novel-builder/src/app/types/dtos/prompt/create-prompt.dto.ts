import { PromptType } from '../../enums/prompt-type';
import { PromptMessageDto } from './prompt-message.dto';

export interface CreatePromptDto {
  name: string;
  type: PromptType;
  messages: PromptMessageDto[];
}
