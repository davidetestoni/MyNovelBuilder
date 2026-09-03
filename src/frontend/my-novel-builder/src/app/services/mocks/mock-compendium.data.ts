import { loremIpsum } from 'lorem-ipsum';
import { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import { CompendiumRecordDto } from '../../types/dtos/compendium-record/compendium-record.dto';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { indexToGuid } from './mock-utils';

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
    contextTokenCount: 42,
    alwaysIncluded: false,
    characterVoiceAssignments: [],
    media: [
      {
        id: indexToGuid(11),
        url: 'https://picsum.photos/seed/11/200/300',
        isCurrent: true,
        isVideo: false,
      },
      {
        id: indexToGuid(12),
        url: 'https://picsum.photos/seed/12/200/300',
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
    contextTokenCount: 38,
    alwaysIncluded: false,
    characterVoiceAssignments: [],
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
    contextTokenCount: 45,
    alwaysIncluded: false,
    characterVoiceAssignments: [],
    media: [
      {
        id: indexToGuid(31),
        url: 'https://picsum.photos/seed/31/200/300',
        isCurrent: true,
        isVideo: false,
      },
      {
        id: indexToGuid(32),
        url: 'https://picsum.photos/seed/32/200/300',
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
    contextTokenCount: 40,
    alwaysIncluded: false,
    characterVoiceAssignments: [],
    media: [
      {
        id: indexToGuid(41),
        url: 'https://picsum.photos/seed/41/200/300',
        isCurrent: true,
        isVideo: false,
      },
      {
        id: indexToGuid(42),
        url: 'https://picsum.photos/seed/42/200/300',
        isCurrent: false,
        isVideo: false,
      },
    ],
    compendiumId: mockedCompendia[0].id,
  },
];
