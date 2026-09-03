import {
  Component,
  EventEmitter,
  HostBinding,
  Input,
  Output,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { PromptType } from '../../types/enums/prompt-type';
import { ModelSelectComponent } from '../model-select/model-select.component';
import { PromptSelectComponent } from '../prompt-select/prompt-select.component';

export type ProseRpgAction = 'do' | 'say';

export interface ProseRpgCommand {
  action: ProseRpgAction;
  input: string;
  promptId: string;
  model: string;
}

@Component({
  selector: 'app-prose-rpg-panel',
  standalone: true,
  templateUrl: './prose-rpg-panel.component.html',
  styleUrl: './prose-rpg-panel.component.scss',
  imports: [
    FormsModule,
    TextareaModule,
    TooltipModule,
    PromptSelectComponent,
    ModelSelectComponent,
  ],
})
export class ProseRpgPanelComponent {
  @Input() visible = true;
  @Input() prompts: PromptDto[] = [];
  @Input() isGenerating = false;
  @Input() isLastChapterSelected = true;
  @Output() promptSubmitted = new EventEmitter<ProseRpgCommand>();
  @Output() promptPreviewed = new EventEmitter<ProseRpgCommand>();

  readonly promptType = PromptType.GenerateText;
  action: ProseRpgAction = 'do';
  input = '';
  selectedPromptId: string | null = null;
  selectedModel: string | null = null;
  promptCount = -1;

  @HostBinding('class.hidden')
  get isHidden(): boolean {
    return !this.visible;
  }

  setAction(action: ProseRpgAction): void {
    this.action = action;
  }

  onPromptOptionsChanged(count: number): void {
    this.promptCount = count;
  }

  isInputDisabled(): boolean {
    return this.isGenerating || !this.isLastChapterSelected;
  }

  getInputPlaceholder(): string {
    return this.isLastChapterSelected
      ? 'Guide the next beat...'
      : 'Go to the last chapter for RPG mode';
  }

  isSendDisabled(): boolean {
    return (
      this.isInputDisabled() ||
      this.promptCount === 0 ||
      !this.input.trim() ||
      !this.selectedPromptId ||
      !this.selectedModel
    );
  }

  submitPrompt(): void {
    if (this.isSendDisabled() || !this.selectedPromptId || !this.selectedModel) {
      return;
    }

    this.promptSubmitted.emit({
      action: this.action,
      input: this.input.trim(),
      promptId: this.selectedPromptId,
      model: this.selectedModel,
    });
  }

  previewPrompt(): void {
    if (this.isSendDisabled() || !this.selectedPromptId || !this.selectedModel) {
      return;
    }

    this.promptPreviewed.emit({
      action: this.action,
      input: this.input.trim(),
      promptId: this.selectedPromptId,
      model: this.selectedModel,
    });
  }

  onEnter(event: Event): void {
    event.preventDefault();
    this.submitPrompt();
  }

  clearInput(): void {
    this.input = '';
  }

  restoreInput(input: string): void {
    this.input = input;
  }
}
