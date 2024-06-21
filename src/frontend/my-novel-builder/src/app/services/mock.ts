import { Observable, of } from 'rxjs';
import { NovelDto } from '../types/dtos/novel/novel.dto';
import { WritingTense } from '../types/enums/writing-tense';
import { WritingPov } from '../types/enums/writing-pov';
import { WritingLanguage } from '../types/enums/writing-language';

export function mockObservable<T>(value: T): Observable<T> {
  return of(value);
}

export const mockedNovels: NovelDto[] = Array(10)
  .fill(0)
  .map((_, index) => ({
    id: (index + 1).toString(),
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
