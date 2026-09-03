import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environment';
import {
  IntegrationsConfigDto,
  UpdateIntegrationsConfigDto,
} from '../types/dtos/integrations/integrations-config.dto';
import { IntegrationsService } from './integrations.service';

@Injectable()
export class ApiIntegrationsService extends IntegrationsService {
  private http = inject(HttpClient);

  private baseUrl = environment.api.baseUrl;

  getIntegrationsConfig(): Observable<IntegrationsConfigDto> {
    return this.http.get<IntegrationsConfigDto>(`${this.baseUrl}/integrations/config`);
  }

  updateIntegrationsConfig(dto: UpdateIntegrationsConfigDto): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/integrations/config`, dto);
  }
}
