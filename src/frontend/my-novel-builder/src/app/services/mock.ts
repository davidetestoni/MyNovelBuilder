import { Observable, of } from 'rxjs';
import { NovelDto } from '../types/dtos/novel/novel.dto';
import { WritingTense } from '../types/enums/writing-tense';
import { WritingPov } from '../types/enums/writing-pov';
import { WritingLanguage } from '../types/enums/writing-language';
import { CompendiumDto } from '../types/dtos/compendium/compendium.dto';
import { loremIpsum } from 'lorem-ipsum';
import { CompendiumRecordType } from '../types/enums/compendium-record-type';

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
