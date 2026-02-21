import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CreatePromptDto } from '../types/dtos/prompt/create-prompt.dto';
import { PromptDto } from '../types/dtos/prompt/prompt.dto';
import { UpdatePromptDto } from '../types/dtos/prompt/update-prompt.dto';
import { mockedPrompts } from './mocks/mock-prompt.data';
import { mockObservable } from './mocks/mock-utils';
import { PromptService } from './prompt.service';

@Injectable()
export class MockPromptService extends PromptService {
  getPrompts(): Observable<PromptDto[]> {
    return mockObservable(mockedPrompts);
  }

  createPrompt(_prompt: CreatePromptDto): Observable<PromptDto> {
    return mockObservable(mockedPrompts[0]);
  }

  updatePrompt(_prompt: UpdatePromptDto): Observable<PromptDto> {
    return mockObservable(mockedPrompts[0]);
  }

  deletePrompt(_promptId: string): Observable<void> {
    return mockObservable<void>(undefined);
  }
}
