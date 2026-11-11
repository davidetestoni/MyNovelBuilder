import { getVoicePreviewLanguage, getVoicePreviewText } from './tts-preview.utils';
import { WritingLanguage } from '../types/enums/writing-language';
import { TtsVoiceDto } from '../types/dtos/generate/tts-voice.dto';

describe('Voice preview language', () => {
  const voice: TtsVoiceDto = {
    voiceId: 'voice', name: 'Voice', previewUrl: null, language: WritingLanguage.English,
  };

  it('uses the preference for provider and unknown voices', () => {
    expect(getVoicePreviewLanguage(voice, WritingLanguage.Italian)).toBe(WritingLanguage.Italian);
    expect(getVoicePreviewLanguage(undefined, WritingLanguage.French)).toBe(WritingLanguage.French);
    expect(getVoicePreviewLanguage({ ...voice, isRegistered: false }, WritingLanguage.Spanish)).toBe(WritingLanguage.Spanish);
  });

  it('always uses a registered voice language, including English', () => {
    expect(getVoicePreviewLanguage({ ...voice, isRegistered: true }, WritingLanguage.Italian)).toBe(WritingLanguage.English);
    expect(getVoicePreviewLanguage({ ...voice, isRegistered: true, language: WritingLanguage.German }, WritingLanguage.French)).toBe(WritingLanguage.German);
  });

  it('defaults existing configurations to English', () => {
    expect(getVoicePreviewLanguage(voice, undefined)).toBe(WritingLanguage.English);
    expect(getVoicePreviewLanguage(undefined, null)).toBe(WritingLanguage.English);
  });

  it('provides a localized sample for every selectable language', () => {
    const samples = Object.values(WritingLanguage).map(getVoicePreviewText);
    expect(new Set(samples).size).toBe(Object.values(WritingLanguage).length);
    expect(samples.every(sample => sample.length > 0)).toBeTrue();
  });
});
