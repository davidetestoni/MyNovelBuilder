import { TextGenerationProvider } from '../../enums/text-generation-provider';
import { TtsProvider } from '../../enums/tts-provider';

export interface IntegrationsConfigDto {
  hasOpenRouterApiKey: boolean;
  hasGoogleGenAiApiKey: boolean;
  textGenerationProvider: TextGenerationProvider;
  ttsProvider: TtsProvider;
}

export interface UpdateIntegrationsConfigDto {
  openRouterApiKey?: string | null;
  googleGenAiApiKey?: string | null;
  textGenerationProvider?: TextGenerationProvider | null;
  ttsProvider?: TtsProvider | null;
}