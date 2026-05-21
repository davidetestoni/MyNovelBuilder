import { IntegrationsConfigDto } from '../../types/dtos/integrations/integrations-config.dto';
import { ImageGenerationProvider } from '../../types/enums/image-generation-provider';
import { TextGenerationProvider } from '../../types/enums/text-generation-provider';
import { TtsProvider } from '../../types/enums/tts-provider';
import { VideoGenerationProvider } from '../../types/enums/video-generation-provider';

export const mockedIntegrationsConfig: IntegrationsConfigDto = {
  hasOpenRouterApiKey: true,
  hasGoogleGenAiApiKey: false,
  hasElevenLabsApiKey: false,
  hasUnrealSpeechApiKey: false,
  hasDeApiApiKey: false,
  hasNanoGptApiKey: false,
  customTtsBaseUrl: 'http://localhost:5000/',
  pocketTtsBaseUrl: 'http://localhost:8000/',
  vibeVoiceBaseUrl: 'http://localhost:8000/',
  chatterboxBaseUrl: 'http://localhost:8000/',
  qwen3BaseUrl: 'http://localhost:8000/',
  omniVoiceBaseUrl: 'http://localhost:8000/',
  textGenerationProvider: TextGenerationProvider.OpenRouter,
  textGenerationModelId: 'openrouter/auto',
  ttsProvider: TtsProvider.Custom,
  imageGenerationProvider: ImageGenerationProvider.DeApi,
  videoGenerationProvider: VideoGenerationProvider.DeApi,
  ttsModelId: 'model-1',
  ttsVoiceId: 'voice-1',
  ttsEnableTextEmphasis: false,
  ttsEnableImmersive: false,
  ttsImmersivePauseMs: 150,
};
