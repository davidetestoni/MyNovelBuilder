import { Observable, of } from 'rxjs';
import { NovelDto } from '../types/dtos/novel/novel.dto';
import { WritingTense } from '../types/enums/writing-tense';
import { WritingPov } from '../types/enums/writing-pov';
import { WritingLanguage } from '../types/enums/writing-language';

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
    id: indexToGuid(index),
    createdAt: '2021-01-01T00:00:00Z',
    updatedAt: '2021-01-01T00:00:00Z',
    title: 'The Great Novel',
    author: 'John Doe',
    brief: 'A novel about nothing',
    tense: WritingTense.Present,
    pov: WritingPov.FirstPerson,
    language: WritingLanguage.English,
    mainCharacterId: null,
    compendiumIds: [],
  }));
