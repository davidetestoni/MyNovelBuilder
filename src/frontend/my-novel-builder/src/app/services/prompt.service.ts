import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environment';
import { mockObservable, mockedPrompts } from './mock';
import { Injectable } from '@angular/core';
import { PromptDto } from '../types/dtos/prompt/prompt.dto';
import { CreatePromptDto } from '../types/dtos/prompt/create-prompt.dto';
import { UpdatePromptDto } from '../types/dtos/prompt/update-prompt.dto';
import { PromptType } from '../types/enums/prompt-type';

@Injectable({
  providedIn: 'root',
})
export class PromptService {
  private baseUrl = environment.api.baseUrl;
  private mocked = environment.mocked;
  private recentInstructionsKey = 'recentInstructions';
  private recentPromptsKey = 'recentPrompts';

  constructor(private http: HttpClient) {}

  getPrompts(): Observable<PromptDto[]> {
    return this.mocked
      ? mockObservable(mockedPrompts)
      : this.http.get<PromptDto[]>(`${this.baseUrl}/prompts`);
  }

  createPrompt(prompt: CreatePromptDto): Observable<PromptDto> {
    return this.mocked
      ? mockObservable(mockedPrompts[0])
      : this.http.post<PromptDto>(`${this.baseUrl}/prompt`, prompt);
  }

  updatePrompt(prompt: UpdatePromptDto): Observable<PromptDto> {
    return this.mocked
      ? mockObservable(mockedPrompts[0])
      : this.http.put<PromptDto>(`${this.baseUrl}/prompt`, prompt);
  }

  deletePrompt(promptId: string): Observable<void> {
    if (this.mocked) {
      return mockObservable<void>(undefined);
    }

    return this.http.delete<void>(`${this.baseUrl}/prompt/${promptId}`);
  }
}
