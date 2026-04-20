import { TextGenerationProvider } from '../../enums/text-generation-provider';
import { ImageGenerationProvider } from '../../enums/image-generation-provider';
import { TtsProvider } from '../../enums/tts-provider';

export interface IntegrationsConfigDto {
  hasOpenRouterApiKey: boolean;
  hasGoogleGenAiApiKey: boolean;
  hasElevenLabsApiKey: boolean;
  hasUnrealSpeechApiKey: boolean;
  hasDeApiApiKey: boolean;
  hasNanoGptApiKey: boolean;
  customTtsBaseUrl: string;
  pocketTtsBaseUrl: string;
  vibeVoiceBaseUrl: string;
  chatterboxBaseUrl: string;
  qwen3BaseUrl: string;
  omniVoiceBaseUrl: string;
  textGenerationProvider: TextGenerationProvider;
  textGenerationModelId: string;
  ttsProvider: TtsProvider;
  imageGenerationProvider: ImageGenerationProvider;
  ttsModelId: string;
  ttsVoiceId: string;
  ttsEnableTextEmphasis: boolean;
  ttsEnableImmersive: boolean;
  ttsImmersivePauseMs: number;
}

export interface UpdateIntegrationsConfigDto {
  openRouterApiKey?: string | null;
  googleGenAiApiKey?: string | null;
  elevenLabsApiKey?: string | null;
  unrealSpeechApiKey?: string | null;
  deApiApiKey?: string | null;
  nanoGptApiKey?: string | null;
  customTtsBaseUrl?: string | null;
  pocketTtsBaseUrl?: string | null;
  vibeVoiceBaseUrl?: string | null;
  chatterboxBaseUrl?: string | null;
  qwen3BaseUrl?: string | null;
  omniVoiceBaseUrl?: string | null;
  textGenerationProvider?: TextGenerationProvider | null;
  textGenerationModelId?: string | null;
  ttsProvider?: TtsProvider | null;
  imageGenerationProvider?: ImageGenerationProvider | null;
  ttsModelId?: string | null;
  ttsVoiceId?: string | null;
  ttsEnableTextEmphasis?: boolean | null;
  ttsEnableImmersive?: boolean | null;
  ttsImmersivePauseMs?: number | null;
}
