import { loremIpsum } from 'lorem-ipsum';
import { NovelDto } from '../../types/dtos/novel/novel.dto';
import { Prose } from '../../types/dtos/novel/prose';
import { WritingLanguage } from '../../types/enums/writing-language';
import { WritingPov } from '../../types/enums/writing-pov';
import { WritingTense } from '../../types/enums/writing-tense';
import { indexToGuid } from './mock-utils';

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
    rpgMode: false,
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
        .map(() => ({
          summary: loremIpsum({ count: 5, units: 'sentences' }),
          text: '<p>' + loremIpsum({ count: 15, units: 'sentences' }) + '</p>',
          images: [],
          recordOverrides: [],
        })),
      storyEvents: Array(6)
        .fill(0)
        .map((__, storyIndex) => ({
          title: `Story Event ${storyIndex + 1}`,
          date: `Day ${index * 6 + storyIndex + 1}`,
          description: loremIpsum({ count: 1, units: 'sentences' }),
        })),
    })),
};
