import { IntegrationsConfigDto } from '../../types/dtos/integrations/integrations-config.dto';
import { ImageGenerationProvider } from '../../types/enums/image-generation-provider';
import { TextGenerationProvider } from '../../types/enums/text-generation-provider';
import { TtsProvider } from '../../types/enums/tts-provider';

export const mockedIntegrationsConfig: IntegrationsConfigDto = {
  hasOpenRouterApiKey: true,
  hasGoogleGenAiApiKey: false,
  hasElevenLabsApiKey: false,
  hasUnrealSpeechApiKey: false,
  hasDeApiApiKey: false,
  hasNanoGptApiKey: false,
  textGenerationProvider: TextGenerationProvider.OpenRouter,
  ttsProvider: TtsProvider.Custom,
  imageGenerationProvider: ImageGenerationProvider.DeApi,
  ttsModelId: 'model-1',
  ttsVoiceId: 'voice-1',
  ttsEnableTextEmphasis: false,
};
