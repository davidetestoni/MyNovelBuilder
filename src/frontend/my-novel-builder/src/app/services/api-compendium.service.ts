import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environment';
import { CreateCompendiumDto } from '../types/dtos/compendium/create-compendium.dto';
import { CompendiumDto } from '../types/dtos/compendium/compendium.dto';
import { UpdateCompendiumDto } from '../types/dtos/compendium/update-compendium.dto';
import { CreateCompendiumRecordDto } from '../types/dtos/compendium-record/create-compendium-record.dto';
import { CompendiumRecordDto } from '../types/dtos/compendium-record/compendium-record.dto';
import { UpdateCompendiumRecordDto } from '../types/dtos/compendium-record/update-compendium-record.dto';
import { CompendiumService } from './compendium.service';

@Injectable()
export class ApiCompendiumService extends CompendiumService {
  private http = inject(HttpClient);

  private baseUrl = environment.api.baseUrl;

  getCompendia(): Observable<CompendiumDto[]> {
    return this.http.get<CompendiumDto[]>(`${this.baseUrl}/compendia`);
  }

  getCompendium(compendiumId: string): Observable<CompendiumDto> {
    return this.http.get<CompendiumDto>(`${this.baseUrl}/compendium/${compendiumId}`);
  }

  getRecords(compendiumId: string): Observable<CompendiumRecordDto[]> {
    return this.http.get<CompendiumRecordDto[]>(`${this.baseUrl}/compendium-records`, {
      params: {
        compendiumId,
      },
    });
  }

  getRecordsByIds(recordIds: string[]): Observable<CompendiumRecordDto[]> {
    return this.http.post<CompendiumRecordDto[]>(
      `${this.baseUrl}/compendium-records/by-ids`,
      recordIds,
    );
  }

  getRecord(recordId: string): Observable<CompendiumRecordDto> {
    return this.http.get<CompendiumRecordDto>(`${this.baseUrl}/compendium-record/${recordId}`);
  }

  createCompendium(compendium: CreateCompendiumDto): Observable<CompendiumDto> {
    return this.http.post<CompendiumDto>(`${this.baseUrl}/compendium`, compendium);
  }

  createRecord(record: CreateCompendiumRecordDto): Observable<CompendiumRecordDto> {
    return this.http.post<CompendiumRecordDto>(`${this.baseUrl}/compendium-record`, record);
  }

  updateCompendium(compendium: UpdateCompendiumDto): Observable<CompendiumDto> {
    return this.http.put<CompendiumDto>(`${this.baseUrl}/compendium`, compendium);
  }

  updateRecord(record: UpdateCompendiumRecordDto): Observable<CompendiumRecordDto> {
    return this.http.put<CompendiumRecordDto>(`${this.baseUrl}/compendium-record`, record);
  }

  deleteCompendium(compendiumId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/compendium/${compendiumId}`);
  }

  deleteRecord(recordId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/compendium-record/${recordId}`);
  }

  uploadRecordMedia(recordId: string, file: File | Blob, isCurrent: boolean): Observable<void> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('isCurrent', isCurrent.toString());

    return this.http.post<void>(`${this.baseUrl}/compendium-record/${recordId}/media`, formData);
  }

  deleteRecordMedia(recordId: string, mediaId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/compendium-record/${recordId}/media/${mediaId}`);
  }

  setCurrentRecordImage(recordId: string, imageId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/compendium-record/${recordId}/image/${imageId}/set-current`, {});
  }
}
