import { TtsProvider } from '../../enums/tts-provider';

export interface TtsRequestDto {
  message: string;
  modelId?: string;
  voiceId?: string;
  gainDb?: number | null;
  provider?: TtsProvider;
}
