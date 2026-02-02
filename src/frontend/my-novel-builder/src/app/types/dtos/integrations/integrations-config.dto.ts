import { TextGenerationProvider } from '../../enums/text-generation-provider';
import { TtsProvider } from '../../enums/tts-provider';

export interface IntegrationsConfigDto {
  hasOpenRouterApiKey: boolean;
  hasGoogleGenAiApiKey: boolean;
  hasElevenLabsApiKey: boolean;
  hasUnrealSpeechApiKey: boolean;
  textGenerationProvider: TextGenerationProvider;
  ttsProvider: TtsProvider;
  ttsVoiceId: string;
}

export interface UpdateIntegrationsConfigDto {
  openRouterApiKey?: string | null;
  googleGenAiApiKey?: string | null;
  elevenLabsApiKey?: string | null;
  unrealSpeechApiKey?: string | null;
  textGenerationProvider?: TextGenerationProvider | null;
  ttsProvider?: TtsProvider | null;
  ttsVoiceId?: string | null;
}
