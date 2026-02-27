import { TextGenerationProvider } from '../../enums/text-generation-provider';
import { TtsProvider } from '../../enums/tts-provider';
import { ImageGenerationProvider } from '../../enums/image-generation-provider';

export interface IntegrationsConfigDto {
  hasOpenRouterApiKey: boolean;
  hasGoogleGenAiApiKey: boolean;
  hasElevenLabsApiKey: boolean;
  hasUnrealSpeechApiKey: boolean;
  hasDeApiApiKey: boolean;
  hasNanoGptApiKey: boolean;
  textGenerationProvider: TextGenerationProvider;
  ttsProvider: TtsProvider;
  imageGenerationProvider: ImageGenerationProvider;
  ttsVoiceId: string;
}

export interface UpdateIntegrationsConfigDto {
  openRouterApiKey?: string | null;
  googleGenAiApiKey?: string | null;
  elevenLabsApiKey?: string | null;
  unrealSpeechApiKey?: string | null;
  deApiApiKey?: string | null;
  nanoGptApiKey?: string | null;
  textGenerationProvider?: TextGenerationProvider | null;
  ttsProvider?: TtsProvider | null;
  imageGenerationProvider?: ImageGenerationProvider | null;
  ttsVoiceId?: string | null;
}
