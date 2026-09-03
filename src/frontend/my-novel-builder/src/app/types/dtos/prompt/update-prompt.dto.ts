import { PromptType } from '../../enums/prompt-type';
import { PromptMessageDto } from './prompt-message.dto';

export interface UpdatePromptDto {
  id: string;
  name: string;
  type: PromptType;
  messages: PromptMessageDto[];
}
