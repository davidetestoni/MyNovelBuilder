import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CompendiumDto } from '../types/dtos/compendium/compendium.dto';
import { CreateCompendiumDto } from '../types/dtos/compendium/create-compendium.dto';
import { UpdateCompendiumDto } from '../types/dtos/compendium/update-compendium.dto';
import { CompendiumRecordDto } from '../types/dtos/compendium-record/compendium-record.dto';
import { CreateCompendiumRecordDto } from '../types/dtos/compendium-record/create-compendium-record.dto';
import { UpdateCompendiumRecordDto } from '../types/dtos/compendium-record/update-compendium-record.dto';

@Injectable()
export abstract class CompendiumService {
  abstract getCompendia(): Observable<CompendiumDto[]>;
  abstract getCompendium(compendiumId: string): Observable<CompendiumDto>;
  abstract getRecords(compendiumId: string): Observable<CompendiumRecordDto[]>;
  abstract getRecordsByIds(recordIds: string[]): Observable<CompendiumRecordDto[]>;
  abstract getRecord(recordId: string): Observable<CompendiumRecordDto>;
  abstract createCompendium(compendium: CreateCompendiumDto): Observable<CompendiumDto>;
  abstract createRecord(record: CreateCompendiumRecordDto): Observable<CompendiumRecordDto>;
  abstract updateCompendium(compendium: UpdateCompendiumDto): Observable<CompendiumDto>;
  abstract updateRecord(record: UpdateCompendiumRecordDto): Observable<CompendiumRecordDto>;
  abstract deleteCompendium(compendiumId: string): Observable<void>;
  abstract deleteRecord(recordId: string): Observable<void>;
  abstract uploadRecordMedia(recordId: string, file: File | Blob, isCurrent: boolean): Observable<void>;
  abstract deleteRecordMedia(recordId: string, mediaId: string): Observable<void>;
  abstract setCurrentRecordImage(recordId: string, imageId: string): Observable<void>;
}
