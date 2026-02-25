import { VoiceDto } from '../../types/dtos/voice/voice.dto';
import { VoiceGender } from '../../types/enums/voice-gender';
import { indexToGuid } from './mock-utils';

export const mockedVoices: VoiceDto[] = [
  {
    id: indexToGuid(11),
    createdAt: '2026-02-01T10:21:00Z',
    updatedAt: '2026-02-01T10:21:00Z',
    name: 'Narrator One',
    voiceGender: VoiceGender.Both,
  },
  {
    id: indexToGuid(12),
    createdAt: '2026-02-03T09:08:00Z',
    updatedAt: '2026-02-03T09:08:00Z',
    name: 'Deep Male',
    voiceGender: VoiceGender.Male,
  },
  {
    id: indexToGuid(13),
    createdAt: '2026-02-05T15:40:00Z',
    updatedAt: '2026-02-05T15:40:00Z',
    name: 'Soft Female',
    voiceGender: VoiceGender.Female,
  },
];
