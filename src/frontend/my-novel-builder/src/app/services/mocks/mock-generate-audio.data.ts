import { TtsModelDto } from '../../types/dtos/generate/tts-model.dto';
import { WritingLanguage } from '../../types/enums/writing-language';

export const mockedAvailableModels: TtsModelDto[] = [
  {
    modelId: 'model-1',
    name: 'Model 1',
    supportsTextEmphasis: false,
    voices: [
      {
        voiceId: '1',
        name: 'Voice 1',
        previewUrl: 'https://example.com/voice1',
        language: WritingLanguage.English,
      },
      {
        voiceId: '2',
        name: 'Voice 2',
        previewUrl: 'https://example.com/voice2',
        language: WritingLanguage.English,
      },
    ],
  },
  {
    modelId: 'model-2',
    name: 'Model 2',
    supportsTextEmphasis: false,
    voices: [
      {
        voiceId: '3',
        name: 'Voice 3',
        previewUrl: 'https://example.com/voice3',
        language: WritingLanguage.English,
      },
    ],
  },
];
