import { loremIpsum } from 'lorem-ipsum';
import { Chat } from '../../types/dtos/chats/chat';
import { ChatMetadata } from '../../types/dtos/chats/chat-metadata';
import { ChatMessageRole } from '../../types/enums/chat-message-role';
import { indexToGuid } from './mock-utils';

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
