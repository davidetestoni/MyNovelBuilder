import { TtsProvider } from '../../enums/tts-provider';
import { WritingLanguage } from '../../enums/writing-language';

export interface VoiceDesignRequestDto {
  provider: TtsProvider;
  prompt: string;
  language: WritingLanguage;
  voiceDescription: string;
}
