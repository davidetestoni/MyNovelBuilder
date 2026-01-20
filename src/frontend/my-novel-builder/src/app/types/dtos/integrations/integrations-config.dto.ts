import { TtsProvider } from '../../enums/tts-provider';

export interface IntegrationsConfigDto {
  hasOpenRouterApiKey: boolean;
  ttsProvider: TtsProvider;
}

export interface UpdateIntegrationsConfigDto {
  openRouterApiKey?: string | null;
  ttsProvider?: TtsProvider | null;
}
