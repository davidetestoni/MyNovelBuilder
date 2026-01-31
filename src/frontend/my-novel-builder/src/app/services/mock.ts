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
import { TtsProvider } from '../types/enums/tts-provider';
import { ChatMetadata } from '../types/dtos/chats/chat-metadata';
import { ChatMessageRole } from '../types/enums/chat-message-role';
import { Chat } from '../types/dtos/chats/chat';

export function mockObservable<T>(value: T): Observable<T> {
  return of(value);
}

// Function that hashes an index to a GUID-like string
export function indexToGuid(index: number): string {
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
    compendiumIds: [indexToGuid((index % 3) + 1)],
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
          images: [],
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
  {
    id: indexToGuid(2),
    createdAt: '2021-01-01T00:00:00Z',
    updatedAt: '2021-01-01T00:00:00Z',
    name: 'The Second Prompt',
    type: PromptType.ReplaceText,
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
    ],
  },
  {
    id: indexToGuid(3),
    createdAt: '2021-01-01T00:00:00Z',
    updatedAt: '2021-01-01T00:00:00Z',
    name: 'The Third Prompt',
    type: PromptType.CreateCompendiumRecord,
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
    ],
  },
  {
    id: indexToGuid(4),
    createdAt: '2021-01-01T00:00:00Z',
    updatedAt: '2021-01-01T00:00:00Z',
    name: 'The Fourth Prompt',
    type: PromptType.SummarizeText,
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
    ],
  },
];

export const mockedTextGenerationResponse = (generatedText: string) =>
  new Observable<HttpEvent<string>>(
    (subscriber: Subscriber<HttpEvent<string>>) => {
      setTimeout(() => {
        let index = 0;
        let partialText = '';

        const intervalId = setInterval(() => {
          if (index < generatedText.length) {
            const char = generatedText.charAt(index);
            const chunk: GenerateTextResponseChunkDto = { content: char };

            partialText += JSON.stringify(chunk) + '\n';

            subscriber.next(<HttpDownloadProgressEvent>{
              type: HttpEventType.DownloadProgress,
              loaded: index + 1,
              total: generatedText.length,
              partialText: partialText,
            });

            index++;
          } else {
            clearInterval(intervalId);

            const finalResponse = new HttpResponse({
              body: partialText,
              status: 200,
              statusText: 'OK',
            });
            subscriber.next(finalResponse);
            subscriber.complete();
          }
        }, 50);
      }, 500);
    },
  );

export const mockedImageGenerationResponse = () =>
  new Observable<HttpEvent<Blob>>((subscriber: Subscriber<HttpEvent<Blob>>) => {
    fetch('https://picsum.photos/512/512')
      .then((response) => response.blob())
      .then((blob) => {
        const response = new HttpResponse({
          body: blob,
          status: 200,
          statusText: 'OK',
        });
        subscriber.next(response);
        subscriber.complete();
      })
      .catch((error) => subscriber.error(error));
  });

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
  },
];

export const mockedIntegrationsConfig = {
  hasOpenRouterApiKey: true,
  ttsProvider: TtsProvider.Custom,
};

export const mockedChats: ChatMetadata[] = [
  {
    id: indexToGuid(3),
    createdAt: '2025-01-01T00:00:00Z',
    updatedAt: '2026-01-02T00:00:00Z',
    name: 'Chat about Chapter 1',
  },
  {
    id: indexToGuid(2),
    createdAt: '2024-06-03T00:00:00Z',
    updatedAt: '2024-01-04T00:00:00Z',
    name: 'Brainstorming Session',
  },
  {
    id: indexToGuid(1),
    createdAt: '2023-01-05T00:00:00Z',
    updatedAt: '2023-01-06T00:00:00Z',
    name: null,
  },
];

export const mockedChat: Chat = {
  id: indexToGuid(1),
  createdAt: '2021-01-01T00:00:00Z',
  updatedAt: '2021-01-02T00:00:00Z',
  name: 'Chat about Chapter 1',
  context: {
    novelId: indexToGuid(1),
    chapterIndex: 0,
    compendiumIds: [indexToGuid(1)],
    compendiumRecordIds: [indexToGuid(1), indexToGuid(2)],
  },
  messages: [
    {
      id: indexToGuid(101),
      sentAt: '2021-01-01T01:00:00Z',
      role: ChatMessageRole.User,
      textContent: loremIpsum({ count: 2, units: 'sentences' }),
    },
    {
      id: indexToGuid(102),
      sentAt: '2021-01-01T01:05:00Z',
      role: ChatMessageRole.Assistant,
      textContent: loremIpsum({ count: 2, units: 'paragraphs' }),
    },
    {
      id: indexToGuid(103),
      sentAt: '2021-01-01T01:10:00Z',
      role: ChatMessageRole.User,
      textContent: loremIpsum({ count: 4, units: 'sentences' }),
    },
    {
      id: indexToGuid(104),
      sentAt: '2021-01-01T01:15:00Z',
      role: ChatMessageRole.Assistant,
      textContent: loremIpsum({ count: 1, units: 'paragraphs' }),
    },
    {
      id: indexToGuid(105),
      sentAt: '2021-01-01T01:20:00Z',
      role: ChatMessageRole.User,
      textContent: loremIpsum({ count: 3, units: 'sentences' }),
    },
    {
      id: indexToGuid(106),
      sentAt: '2021-01-01T01:25:00Z',
      role: ChatMessageRole.Assistant,
      textContent: loremIpsum({ count: 3, units: 'paragraphs' }),
    },
  ],
};
