import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environment';
import {
  mockObservable,
  mockedCompendia,
  mockedCompendiumRecords,
} from './mock';
import { Injectable } from '@angular/core';
import { CompendiumDto } from '../types/dtos/compendium/compendium.dto';
import { CreateCompendiumDto } from '../types/dtos/compendium/create-compendium.dto';
import { UpdateCompendiumDto } from '../types/dtos/compendium/update-compendium.dto';
import { CompendiumRecordDto } from '../types/dtos/compendium-record/compendium-record.dto';
import { CreateCompendiumRecordDto } from '../types/dtos/compendium-record/create-compendium-record.dto';
import { UpdateCompendiumRecordDto } from '../types/dtos/compendium-record/update-compendium-record.dto';

@Injectable({
  providedIn: 'root',
})
export class CompendiumService {
  private baseUrl = environment.api.baseUrl;
  private mocked = environment.mocked;

  constructor(private http: HttpClient) {}

  getCompendia(): Observable<CompendiumDto[]> {
    return this.mocked
      ? mockObservable(mockedCompendia)
      : this.http.get<CompendiumDto[]>(`${this.baseUrl}/compendia`);
  }

  getCompendium(compendiumId: string): Observable<CompendiumDto> {
    return this.mocked
      ? mockObservable(mockedCompendia[0])
      : this.http.get<CompendiumDto>(
          `${this.baseUrl}/compendium/${compendiumId}`
        );
  }

  getRecords(compendiumId: string): Observable<CompendiumRecordDto[]> {
    return this.mocked
      ? mockObservable(mockedCompendiumRecords)
      : this.http.get<CompendiumRecordDto[]>(
          `${this.baseUrl}/compendium-records`,
          {
            params: {
              compendiumId,
            },
          }
        );
  }

  getRecord(recordId: string): Observable<CompendiumRecordDto> {
    return this.mocked
      ? mockObservable(mockedCompendiumRecords[0])
      : this.http.get<CompendiumRecordDto>(
          `${this.baseUrl}/compendium-record/${recordId}`
        );
  }

  createCompendium(compendium: CreateCompendiumDto): Observable<CompendiumDto> {
    return this.mocked
      ? mockObservable(mockedCompendia[0])
      : this.http.post<CompendiumDto>(`${this.baseUrl}/compendium`, compendium);
  }

  createRecord(
    record: CreateCompendiumRecordDto
  ): Observable<CompendiumRecordDto> {
    return this.mocked
      ? mockObservable(mockedCompendiumRecords[0])
      : this.http.post<CompendiumRecordDto>(
          `${this.baseUrl}/compendium-record`,
          record
        );
  }

  updateCompendium(compendium: UpdateCompendiumDto): Observable<CompendiumDto> {
    return this.mocked
      ? mockObservable(mockedCompendia[0])
      : this.http.put<CompendiumDto>(`${this.baseUrl}/compendium`, compendium);
  }

  updateRecord(
    record: UpdateCompendiumRecordDto
  ): Observable<CompendiumRecordDto> {
    return this.mocked
      ? mockObservable(mockedCompendiumRecords[0])
      : this.http.put<CompendiumRecordDto>(
          `${this.baseUrl}/compendium-record`,
          record
        );
  }

  deleteCompendium(compendiumId: string): Observable<void> {
    if (this.mocked) {
      return mockObservable<void>(undefined);
    }

    return this.http.delete<void>(`${this.baseUrl}/compendium/${compendiumId}`);
  }

  deleteRecord(recordId: string): Observable<void> {
    if (this.mocked) {
      return mockObservable<void>(undefined);
    }

    return this.http.delete<void>(
      `${this.baseUrl}/compendium-record/${recordId}`
    );
  }

  uploadRecordImage(
    recordId: string,
    file: File | Blob,
    isCurrent: boolean
  ): Observable<void> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('isCurrent', isCurrent.toString());

    return this.mocked
      ? mockObservable<void>(undefined)
      : this.http.post<void>(
          `${this.baseUrl}/compendium-record/${recordId}/image`,
          formData
        );
  }

  deleteRecordImage(recordId: string, imageId: string): Observable<void> {
    return this.mocked
      ? mockObservable<void>(undefined)
      : this.http.delete<void>(
          `${this.baseUrl}/compendium-record/${recordId}/image/${imageId}`
        );
  }

  setCurrentRecordImage(recordId: string, imageId: string): Observable<void> {
    return this.mocked
      ? mockObservable<void>(undefined)
      : this.http.post<void>(
          `${this.baseUrl}/compendium-record/${recordId}/image/${imageId}/set-current`,
          {}
        );
  }
}
