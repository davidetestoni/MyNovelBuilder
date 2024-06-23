import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environment';
import { mockObservable, mockedCompendia } from './mock';
import { Injectable } from '@angular/core';
import { CompendiumDto } from '../types/dtos/compendium/compendium.dto';
import { CreateCompendiumDto } from '../types/dtos/compendium/create-compendium.dto';
import { UpdateCompendiumDto } from '../types/dtos/compendium/update-compendium.dto';

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

  createCompendium(
    compendium: CreateCompendiumDto
  ): Observable<CreateCompendiumDto> {
    return this.mocked
      ? mockObservable(mockedCompendia[0])
      : this.http.post<CreateCompendiumDto>(
          `${this.baseUrl}/compendium`,
          compendium
        );
  }

  updateCompendium(
    compendium: UpdateCompendiumDto
  ): Observable<UpdateCompendiumDto> {
    return this.mocked
      ? mockObservable(mockedCompendia[0])
      : this.http.put<UpdateCompendiumDto>(
          `${this.baseUrl}/compendium`,
          compendium
        );
  }

  deleteCompendium(compendiumId: string): Observable<void> {
    if (this.mocked) {
      return mockObservable<void>(undefined);
    }

    return this.http.delete<void>(`${this.baseUrl}/compendium/${compendiumId}`);
  }
}
