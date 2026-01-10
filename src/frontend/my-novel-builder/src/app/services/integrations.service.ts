import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environment';
import {
  IntegrationsConfigDto,
  UpdateIntegrationsConfigDto,
} from '../types/dtos/integrations/integrations-config.dto';
import { mockedIntegrationsConfig, mockObservable } from './mock';

@Injectable({
  providedIn: 'root',
})
export class IntegrationsService {
  private http = inject(HttpClient);

  private baseUrl = environment.api.baseUrl;
  private mocked = environment.mocked;

  getIntegrationsConfig(): Observable<IntegrationsConfigDto> {
    return this.mocked
      ? mockObservable<IntegrationsConfigDto>(mockedIntegrationsConfig)
      : this.http.get<IntegrationsConfigDto>(
          `${this.baseUrl}/integrations/config`
        );
  }

  updateIntegrationsConfig(dto: UpdateIntegrationsConfigDto): Observable<void> {
    return this.mocked
      ? mockObservable<void>(undefined)
      : this.http.put<void>(`${this.baseUrl}/integrations/config`, dto);
  }
}
