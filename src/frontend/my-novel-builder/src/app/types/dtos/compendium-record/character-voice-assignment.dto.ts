import { TtsProvider } from '../../enums/tts-provider';

export interface CharacterVoiceAssignmentDto {
  provider: TtsProvider;
  modelId: string;
  voiceId: string;
  voiceName: string | null;
  updatedAt: string;
}
