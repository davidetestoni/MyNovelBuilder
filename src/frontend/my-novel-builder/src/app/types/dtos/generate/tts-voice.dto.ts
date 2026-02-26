import { WritingLanguage } from '../../enums/writing-language';

export interface TtsVoiceDto {
    voiceId: string;
    name: string;
    previewUrl: string | null;
    language: WritingLanguage;
}
