import { VoiceGender } from '../../enums/voice-gender';

export interface VoiceDto {
  id: string;
  createdAt: string;
  updatedAt: string;
  name: string;
  voiceGender: VoiceGender;
}
