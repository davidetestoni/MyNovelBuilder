import { VoiceGender } from '../../enums/voice-gender';
import { WritingLanguage } from '../../enums/writing-language';

export interface VoiceDto {
  id: string;
  createdAt: string;
  updatedAt: string;
  name: string;
  voiceGender: VoiceGender;
  language: WritingLanguage;
}
