import { TtsVoiceDto } from './tts-voice.dto';

export interface TtsModelDto {
  modelId: string;
  name: string;
  voices: TtsVoiceDto[];
}
