import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { PromptService } from '../../services/prompt.service';
import { PromptType } from '../../types/enums/prompt-type';
import { CreatePromptComponent } from '../../components/create-prompt/create-prompt.component';
import { SpacedPipe } from '../../pipes/spaced.pipe';
import { PromptComponent } from '../../components/prompt/prompt.component';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-prompts',
  standalone: true,
  templateUrl: './prompts.component.html',
  styleUrl: './prompts.component.scss',
  imports: [FormsModule, SpacedPipe, PromptComponent, ButtonModule],
  providers: [DialogService],
})
export class PromptsComponent implements OnInit, OnDestroy {
  prompts: PromptDto[] | null = null;
  private dialogService = inject(DialogService);
  private dialogRef: DynamicDialogRef | null = null;
  readonly promptService: PromptService = inject(PromptService);
  currentPrompt: PromptDto | null = null;

  promptTypes: PromptType[] = Object.values(PromptType);

  ngOnInit(): void {
    this.getPrompts();
  }

  ngOnDestroy(): void {
    if (this.dialogRef) {
      this.dialogRef.close();
    }
  }

  getPrompts(): void {
    this.promptService.getPrompts().subscribe((prompts) => {
      this.prompts = prompts;

      // If there was a selected prompt, update it with the latest data
      if (this.currentPrompt) {
        this.currentPrompt =
          prompts.find((p) => p.id === this.currentPrompt?.id) || null;
      }
    });
  }

  setCurrentPrompt(prompt: PromptDto): void {
    this.currentPrompt = prompt;
  }

  openCreatePromptDialog(): void {
    this.dialogRef = this.dialogService.open(CreatePromptComponent, {
      header: 'Create a prompt',
      width: '50vw',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      modal: true,
      closable: true,
      closeOnEscape: true,
      dismissableMask: true,
    });

    this.dialogRef?.onClose.subscribe((prompt: PromptDto) => {
      if (prompt) {
        // Select the newly created prompt, then refresh the prompts
        // (this will also update the current prompt)
        this.currentPrompt = prompt;

        this.getPrompts();
      }
    });
  }

  getPromptsOfType(type: PromptType): PromptDto[] {
    return this.prompts?.filter((p) => p.type === type) || [];
  }

  updatePrompt(prompt: PromptDto): void {
    this.promptService
      .updatePrompt({
        id: prompt.id,
        name: prompt.name,
        type: prompt.type,
        messages: prompt.messages,
      })
      .subscribe();
  }

  deletePrompt(prompt: PromptDto): void {
    this.promptService.deletePrompt(prompt.id).subscribe(() => {
      this.getPrompts();
      this.currentPrompt = null;
    });
  }
}
