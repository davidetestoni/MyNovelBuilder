import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  IntegrationsConfigDto,
  UpdateIntegrationsConfigDto,
} from '../types/dtos/integrations/integrations-config.dto';

@Injectable()
export abstract class IntegrationsService {
  abstract getIntegrationsConfig(): Observable<IntegrationsConfigDto>;
  abstract updateIntegrationsConfig(
    dto: UpdateIntegrationsConfigDto,
  ): Observable<void>;
}
