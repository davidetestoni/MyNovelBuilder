import { TtsProvider } from '../../enums/tts-provider';

export interface TtsRequestDto {
  message: string;
  voiceId?: string;
  provider?: TtsProvider;
}
