import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environment';
import { mockObservable, mockedPrompts } from './mock';
import { Injectable } from '@angular/core';
import { PromptDto } from '../types/dtos/prompt/prompt.dto';
import { CreatePromptDto } from '../types/dtos/prompt/create-prompt.dto';
import { UpdatePromptDto } from '../types/dtos/prompt/update-prompt.dto';
import { PromptType } from '../types/enums/prompt-type';

// TODO: Make a generic service to interact with local storage
interface RecentInstructions {
  [key: string]: string;
}

interface RecentPrompts {
  [key: string]: string;
}

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

  private getRecentInstructions(): RecentInstructions {
    const recentInstructionsString = localStorage.getItem(
      this.recentInstructionsKey
    );
    return recentInstructionsString ? JSON.parse(recentInstructionsString) : {};
  }

  getRecentInstructionsForPromptType(promptType: PromptType): string | null {
    const recentInstructions = this.getRecentInstructions();
    return recentInstructions[promptType] || null;
  }

  setRecentInstructionsForPromptType(
    promptType: PromptType,
    instruction: string
  ): void {
    const recentInstructions = this.getRecentInstructions();
    recentInstructions[promptType] = instruction;
    localStorage.setItem(
      this.recentInstructionsKey,
      JSON.stringify(recentInstructions)
    );
  }

  private getRecentPrompts(): RecentPrompts {
    const recentPromptsString = localStorage.getItem(
      this.recentPromptsKey
    );
    return recentPromptsString ? JSON.parse(recentPromptsString) : {};
  }

  getRecentPromptForPromptType(promptType: PromptType): string | null {
    const recentPrompts = this.getRecentPrompts();
    return recentPrompts[promptType] || null;
  }

  setRecentPromptForPromptType(
    promptType: PromptType,
    promptId: string
  ): void {
    const recentPrompts = this.getRecentPrompts();
    recentPrompts[promptType] = promptId;
    localStorage.setItem(
      this.recentPromptsKey,
      JSON.stringify(recentPrompts)
    );
  }
}
