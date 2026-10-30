import { TtsProvider } from '../../enums/tts-provider';

export interface CharacterVoiceAssignmentDto {
  provider: TtsProvider;
  modelId: string;
  voiceId: string;
  gainDb?: number | null;
  voiceName: string | null;
  updatedAt: string;
}
