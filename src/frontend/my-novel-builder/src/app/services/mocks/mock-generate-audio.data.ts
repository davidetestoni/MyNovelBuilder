import { TtsVoiceDto } from '../../types/dtos/generate/tts-voice.dto';
import { WritingLanguage } from '../../types/enums/writing-language';

export const mockedAvailableVoices: TtsVoiceDto[] = [
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
  {
    voiceId: '3',
    name: 'Voice 3',
    previewUrl: 'https://example.com/voice3',
    language: WritingLanguage.English,
  },
];
