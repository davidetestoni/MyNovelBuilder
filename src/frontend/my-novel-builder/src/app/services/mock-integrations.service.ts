import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  IntegrationsConfigDto,
  UpdateIntegrationsConfigDto,
} from '../types/dtos/integrations/integrations-config.dto';
import { mockedIntegrationsConfig } from './mocks/mock-integrations.data';
import { mockObservable } from './mocks/mock-utils';
import { IntegrationsService } from './integrations.service';

@Injectable()
export class MockIntegrationsService extends IntegrationsService {
  getIntegrationsConfig(): Observable<IntegrationsConfigDto> {
    return mockObservable<IntegrationsConfigDto>(mockedIntegrationsConfig);
  }

  updateIntegrationsConfig(_dto: UpdateIntegrationsConfigDto): Observable<void> {
    return mockObservable<void>(undefined);
  }
}
