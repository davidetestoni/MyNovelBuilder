import { loremIpsum } from 'lorem-ipsum';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { PromptMessageRole } from '../../types/enums/prompt-message-role';
import { PromptType } from '../../types/enums/prompt-type';
import { indexToGuid } from './mock-utils';

export const mockedPrompts: PromptDto[] = [
  {
    id: indexToGuid(1),
    createdAt: '2021-01-01T00:00:00Z',
    updatedAt: '2021-01-01T00:00:00Z',
    name: 'The Great Prompt',
    type: PromptType.GenerateText,
    messages: [
      { id: 0, role: PromptMessageRole.System, message: loremIpsum({ count: 3, units: 'sentences' }) },
      { id: 1, role: PromptMessageRole.User, message: loremIpsum({ count: 3, units: 'sentences' }) },
      { id: 2, role: PromptMessageRole.Assistant, message: loremIpsum({ count: 3, units: 'sentences' }) },
      { id: 3, role: PromptMessageRole.User, message: loremIpsum({ count: 3, units: 'sentences' }) },
    ],
  },
  {
    id: indexToGuid(2),
    createdAt: '2021-01-01T00:00:00Z',
    updatedAt: '2021-01-01T00:00:00Z',
    name: 'The Second Prompt',
    type: PromptType.ReplaceText,
    messages: [
      { id: 0, role: PromptMessageRole.System, message: loremIpsum({ count: 3, units: 'sentences' }) },
      { id: 1, role: PromptMessageRole.User, message: loremIpsum({ count: 3, units: 'sentences' }) },
    ],
  },
  {
    id: indexToGuid(3),
    createdAt: '2021-01-01T00:00:00Z',
    updatedAt: '2021-01-01T00:00:00Z',
    name: 'The Third Prompt',
    type: PromptType.CreateCompendiumRecord,
    messages: [
      { id: 0, role: PromptMessageRole.System, message: loremIpsum({ count: 3, units: 'sentences' }) },
      { id: 1, role: PromptMessageRole.User, message: loremIpsum({ count: 3, units: 'sentences' }) },
    ],
  },
  {
    id: indexToGuid(4),
    createdAt: '2021-01-01T00:00:00Z',
    updatedAt: '2021-01-01T00:00:00Z',
    name: 'The Fourth Prompt',
    type: PromptType.SummarizeText,
    messages: [
      { id: 0, role: PromptMessageRole.System, message: loremIpsum({ count: 3, units: 'sentences' }) },
      { id: 1, role: PromptMessageRole.User, message: loremIpsum({ count: 3, units: 'sentences' }) },
    ],
  },
  {
    id: indexToGuid(5),
    createdAt: '2021-01-01T00:00:00Z',
    updatedAt: '2021-01-01T00:00:00Z',
    name: 'The Fifth Prompt',
    type: PromptType.SendChatMessage,
    messages: [
      { id: 0, role: PromptMessageRole.System, message: loremIpsum({ count: 3, units: 'sentences' }) },
    ],
  },
];
