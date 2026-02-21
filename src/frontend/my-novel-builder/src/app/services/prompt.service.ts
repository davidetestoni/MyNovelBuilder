import { Observable } from 'rxjs';
import { Injectable } from '@angular/core';
import { PromptDto } from '../types/dtos/prompt/prompt.dto';
import { CreatePromptDto } from '../types/dtos/prompt/create-prompt.dto';
import { UpdatePromptDto } from '../types/dtos/prompt/update-prompt.dto';

@Injectable()
export abstract class PromptService {
  abstract getPrompts(): Observable<PromptDto[]>;
  abstract createPrompt(prompt: CreatePromptDto): Observable<PromptDto>;
  abstract updatePrompt(prompt: UpdatePromptDto): Observable<PromptDto>;
  abstract deletePrompt(promptId: string): Observable<void>;
}
