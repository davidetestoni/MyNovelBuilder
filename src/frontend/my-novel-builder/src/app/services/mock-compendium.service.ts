import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateCompendiumDto } from '../types/dtos/compendium/create-compendium.dto';
import { CompendiumDto } from '../types/dtos/compendium/compendium.dto';
import { UpdateCompendiumDto } from '../types/dtos/compendium/update-compendium.dto';
import { CreateCompendiumRecordDto } from '../types/dtos/compendium-record/create-compendium-record.dto';
import { CompendiumRecordDto } from '../types/dtos/compendium-record/compendium-record.dto';
import { UpdateCompendiumRecordDto } from '../types/dtos/compendium-record/update-compendium-record.dto';
import {
  mockedCompendia,
  mockedCompendiumRecords,
} from './mocks/mock-compendium.data';
import { mockedNovels } from './mocks/mock-novel.data';
import { mockObservable } from './mocks/mock-utils';
import { CompendiumService } from './compendium.service';

@Injectable()
export class MockCompendiumService extends CompendiumService {
  getCompendia(): Observable<CompendiumDto[]> {
    return mockObservable(mockedCompendia);
  }

  getNovelCompendia(novelId: string): Observable<CompendiumDto[]> {
    const compendiumIds = new Set(
      mockedNovels.find((novel) => novel.id === novelId)?.compendiumIds ?? [],
    );
    return mockObservable(
      mockedCompendia.filter((compendium) => compendiumIds.has(compendium.id)),
    );
  }

  getCompendium(_compendiumId: string): Observable<CompendiumDto> {
    return mockObservable(mockedCompendia[0]);
  }

  getRecords(_compendiumId: string): Observable<CompendiumRecordDto[]> {
    return mockObservable(mockedCompendiumRecords);
  }

  getRecordsByIds(recordIds: string[]): Observable<CompendiumRecordDto[]> {
    const idSet = new Set(recordIds);
    return mockObservable(mockedCompendiumRecords.filter((r) => idSet.has(r.id)));
  }

  getRecord(_recordId: string): Observable<CompendiumRecordDto> {
    return mockObservable(mockedCompendiumRecords[0]);
  }

  createCompendium(_compendium: CreateCompendiumDto): Observable<CompendiumDto> {
    return mockObservable(mockedCompendia[0]);
  }

  createRecord(_record: CreateCompendiumRecordDto): Observable<CompendiumRecordDto> {
    return mockObservable(mockedCompendiumRecords[0]);
  }

  updateCompendium(_compendium: UpdateCompendiumDto): Observable<CompendiumDto> {
    return mockObservable(mockedCompendia[0]);
  }

  updateRecord(_record: UpdateCompendiumRecordDto): Observable<CompendiumRecordDto> {
    return mockObservable(mockedCompendiumRecords[0]);
  }

  deleteCompendium(_compendiumId: string): Observable<void> {
    return mockObservable<void>(undefined);
  }

  deleteRecord(_recordId: string): Observable<void> {
    return mockObservable<void>(undefined);
  }

  uploadRecordMedia(_recordId: string, _file: File | Blob, _isCurrent: boolean): Observable<void> {
    return mockObservable<void>(undefined);
  }

  deleteRecordMedia(_recordId: string, _mediaId: string): Observable<void> {
    return mockObservable<void>(undefined);
  }

  setCurrentRecordImage(_recordId: string, _imageId: string): Observable<void> {
    return mockObservable<void>(undefined);
  }
}
