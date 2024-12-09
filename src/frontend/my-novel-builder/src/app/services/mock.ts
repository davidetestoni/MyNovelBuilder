import { Observable, Subscriber, of } from 'rxjs';
import { NovelDto } from '../types/dtos/novel/novel.dto';
import { WritingTense } from '../types/enums/writing-tense';
import { WritingPov } from '../types/enums/writing-pov';
import { WritingLanguage } from '../types/enums/writing-language';
import { CompendiumDto } from '../types/dtos/compendium/compendium.dto';
import { loremIpsum } from 'lorem-ipsum';
import { CompendiumRecordType } from '../types/enums/compendium-record-type';
import { CompendiumRecordDto } from '../types/dtos/compendium-record/compendium-record.dto';
import { PromptDto } from '../types/dtos/prompt/prompt.dto';
import { PromptType } from '../types/enums/prompt-type';
import { PromptMessageRole } from '../types/enums/prompt-message-role';
import { Prose } from '../types/dtos/novel/prose';
import {
  HttpDownloadProgressEvent,
  HttpEvent,
  HttpEventType,
  HttpResponse,
} from '@angular/common/http';
import { GenerateTextResponseChunkDto } from '../types/dtos/generate/generate-text-response-chunk.dto';
import { TtsVoiceDto } from '../types/dtos/generate/tts-voice.dto';

export function mockObservable<T>(value: T): Observable<T> {
  return of(value);
}

// Function that hashes an index to a GUID-like string
function indexToGuid(index: number): string {
  const str = index.toString();
  let hash = 0;
  for (let i = 0; i < str.length; i++) {
    const char = str.charCodeAt(i);
    hash = (hash << 5) - hash + char;
    hash |= 0; // Convert to 32bit integer
  }

  // Convert the hash to a GUID-like format
  const hex = (hash >>> 0).toString(16).padStart(8, '0');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-4${hex.slice(12, 15)}-${(
    ((hash >> 16) & 0x3f) |
    0x80
  ).toString(16)}${hex.slice(15, 18)}-${hex.slice(18, 30)}`.toLowerCase();
}

export const mockedNovels: NovelDto[] = Array(10)
  .fill(0)
  .map((_, index) => ({
    id: indexToGuid(index + 1),
    createdAt: '2021-01-01T00:00:00Z',
    updatedAt: '2021-01-01T00:00:00Z',
    title: 'The Great Novel',
    author: 'John Doe',
    brief: 'A novel about nothing',
    coverImageUrl: `https://picsum.photos/seed/${index + 1}/200/300`,
    tense: WritingTense.Present,
    pov: WritingPov.FirstPerson,
    language: WritingLanguage.English,
    mainCharacterId: null,
    compendiumIds: [],
  }));

export const mockedProse: Prose = {
  chapters: Array(3)
    .fill(0)
    .map((_, index) => ({
      title: `Chapter ${index + 1}`,
      sections: Array(3)
        .fill(0)
        .map((_, index) => ({
          summary: loremIpsum({ count: 5, units: 'sentences' }),
          text: '<p>' + loremIpsum({ count: 15, units: 'sentences' }) + '</p>',
        })),
    })),
};

export const mockedCompendia: CompendiumDto[] = Array(3)
  .fill(0)
  .map((_, index) => ({
    id: indexToGuid(index + 1),
    createdAt: '2021-01-01T00:00:00Z',
    updatedAt: '2021-01-01T00:00:00Z',
    name: 'The Great Compendium',
    description: loremIpsum({ count: 3, units: 'sentences' }),
    records: [
      {
        id: indexToGuid(index * 100 + 1),
        name: 'John Doe',
        type: CompendiumRecordType.Character,
        imageUrl: `https://picsum.photos/seed/${index * 100 + 1}/200/300`,
      },
      {
        id: indexToGuid(index * 100 + 2),
        name: 'Jane Doe',
        type: CompendiumRecordType.Character,
        imageUrl: null,
      },
      {
        id: indexToGuid(index * 100 + 3),
        name: 'The Great City',
        type: CompendiumRecordType.Place,
        imageUrl: `https://picsum.photos/seed/${index * 100 + 3}/200/300`,
      },
      {
        id: indexToGuid(index * 100 + 4),
        name: 'The Great Concept',
        type: CompendiumRecordType.Concept,
        imageUrl: `https://picsum.photos/seed/${index * 100 + 4}/200/300`,
      },
    ],
  }));

export const mockedCompendiumRecords: CompendiumRecordDto[] = [
  {
    id: indexToGuid(1),
    createdAt: '2021-01-01T00:00:00Z',
    updatedAt: '2021-01-01T00:00:00Z',
    name: 'John Doe',
    aliases: 'Johnny',
    type: CompendiumRecordType.Character,
    context: loremIpsum({ count: 3, units: 'sentences' }),
    alwaysIncluded: false,
    media: [
      {
        id: indexToGuid(11),
        url: `https://picsum.photos/seed/11/200/300`,
        isCurrent: true,
        isVideo: false,
      },
      {
        id: indexToGuid(12),
        url: `https://picsum.photos/seed/12/200/300`,
        isCurrent: false,
        isVideo: false,
      },
    ],
    compendiumId: mockedCompendia[0].id,
  },
  {
    id: indexToGuid(2),
    createdAt: '2021-01-01T00:00:00Z',
    updatedAt: '2021-01-01T00:00:00Z',
    name: 'Jane Doe',
    aliases: 'Janie',
    type: CompendiumRecordType.Character,
    context: loremIpsum({ count: 3, units: 'sentences' }),
    alwaysIncluded: false,
    media: [],
    compendiumId: mockedCompendia[0].id,
  },
  {
    id: indexToGuid(3),
    createdAt: '2021-01-01T00:00:00Z',
    updatedAt: '2021-01-01T00:00:00Z',
    name: 'The Great City',
    aliases: 'The City',
    type: CompendiumRecordType.Place,
    context: loremIpsum({ count: 3, units: 'sentences' }),
    alwaysIncluded: false,
    media: [
      {
        id: indexToGuid(31),
        url: `https://picsum.photos/seed/31/200/300`,
        isCurrent: true,
        isVideo: false,
      },
      {
        id: indexToGuid(32),
        url: `https://picsum.photos/seed/32/200/300`,
        isCurrent: false,
        isVideo: false,
      },
    ],
    compendiumId: mockedCompendia[0].id,
  },
  {
    id: indexToGuid(4),
    createdAt: '2021-01-01T00:00:00Z',
    updatedAt: '2021-01-01T00:00:00Z',
    name: 'The Great Concept',
    aliases: 'The Concept',
    type: CompendiumRecordType.Concept,
    context: loremIpsum({ count: 3, units: 'sentences' }),
    alwaysIncluded: false,
    media: [
      {
        id: indexToGuid(41),
        url: `https://picsum.photos/seed/41/200/300`,
        isCurrent: true,
        isVideo: false,
      },
      {
        id: indexToGuid(42),
        url: `https://picsum.photos/seed/42/200/300`,
        isCurrent: false,
        isVideo: false,
      },
    ],
    compendiumId: mockedCompendia[0].id,
  },
];

export const mockedPrompts: PromptDto[] = [
  {
    id: indexToGuid(1),
    createdAt: '2021-01-01T00:00:00Z',
    updatedAt: '2021-01-01T00:00:00Z',
    name: 'The Great Prompt',
    type: PromptType.GenerateText,
    messages: [
      {
        id: 0,
        role: PromptMessageRole.System,
        message: loremIpsum({ count: 3, units: 'sentences' }),
      },
      {
        id: 1,
        role: PromptMessageRole.User,
        message: loremIpsum({ count: 3, units: 'sentences' }),
      },
      {
        id: 2,
        role: PromptMessageRole.Assistant,
        message: loremIpsum({ count: 3, units: 'sentences' }),
      },
      {
        id: 3,
        role: PromptMessageRole.User,
        message: loremIpsum({ count: 3, units: 'sentences' }),
      },
    ],
  },
];

export const mockedTextGenerationResponse = (generatedText: string) =>
  new Observable<HttpEvent<string>>(
    (subscriber: Subscriber<HttpEvent<string>>) => {
      setTimeout(() => {
        let index = 0;

        const chunks: GenerateTextResponseChunkDto[] = [];

        const intervalId = setInterval(() => {
          if (index > generatedText.length) {
            clearInterval(intervalId);
            subscriber.complete();
          } else {
            chunks.push(<GenerateTextResponseChunkDto>{
              content: generatedText.slice(index - 1, index),
            });

            subscriber.next(<HttpDownloadProgressEvent>{
              type: HttpEventType.DownloadProgress,
              loaded: index,
              total: 100,
              partialText: chunks
                .map((item) => JSON.stringify(item))
                .join('\n'),
            });
            index++;
          }
        });
      }, 2500);
    }
  );

export const mockedAvailableVoices: TtsVoiceDto[] = [
  {
    voiceId: '1',
    name: 'Voice 1',
    previewUrl: 'https://example.com/voice1',
  },
  {
    voiceId: '2',
    name: 'Voice 2',
    previewUrl: 'https://example.com/voice2',
  },
  {
    voiceId: '3',
    name: 'Voice 3',
    previewUrl: 'https://example.com/voice3',
  }
];
