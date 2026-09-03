import { TtsProvider } from '../../enums/tts-provider';

export interface TtsProviderDto {
  provider: TtsProvider;
  supportsVoiceDesign: boolean;
}
