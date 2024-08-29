import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { MatDialog } from '@angular/material/dialog';
import { PromptService } from '../../services/prompt.service';
import { PromptType } from '../../types/enums/prompt-type';
import { CreatePromptComponent } from '../../components/create-prompt/create-prompt.component';
import { SpacedPipe } from '../../pipes/spaced.pipe';
import { PromptComponent } from '../../components/prompt/prompt.component';

@Component({
  selector: 'app-prompts',
  standalone: true,
  imports: [FormsModule, SpacedPipe, PromptComponent],
  templateUrl: './prompts.component.html',
  styleUrl: './prompts.component.scss',
})
export class PromptsComponent implements OnInit {
  prompts: PromptDto[] | null = null;
  readonly dialog = inject(MatDialog);
  readonly promptService: PromptService = inject(PromptService);
  currentPrompt: PromptDto | null = null;

  promptTypes: PromptType[] = [
    PromptType.GenerateText,
    PromptType.SummarizeText,
    PromptType.ReplaceText,
    PromptType.CreateCompendiumRecord,
    PromptType.EditCompendiumRecord,
  ];

  ngOnInit(): void {
    this.getPrompts();
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
    const dialogRef = this.dialog.open(CreatePromptComponent, {
      minWidth: '50vw',
    });

    dialogRef.afterClosed().subscribe((prompt: PromptDto) => {
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
