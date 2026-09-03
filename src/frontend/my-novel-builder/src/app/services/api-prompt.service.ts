import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environment';
import { CreatePromptDto } from '../types/dtos/prompt/create-prompt.dto';
import { PromptDto } from '../types/dtos/prompt/prompt.dto';
import { UpdatePromptDto } from '../types/dtos/prompt/update-prompt.dto';
import { PromptService } from './prompt.service';

@Injectable()
export class ApiPromptService extends PromptService {
  private http = inject(HttpClient);

  private baseUrl = environment.api.baseUrl;

  getPrompts(): Observable<PromptDto[]> {
    return this.http.get<PromptDto[]>(`${this.baseUrl}/prompts`);
  }

  createPrompt(prompt: CreatePromptDto): Observable<PromptDto> {
    return this.http.post<PromptDto>(`${this.baseUrl}/prompt`, prompt);
  }

  updatePrompt(prompt: UpdatePromptDto): Observable<PromptDto> {
    return this.http.put<PromptDto>(`${this.baseUrl}/prompt`, prompt);
  }

  deletePrompt(promptId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/prompt/${promptId}`);
  }
}
