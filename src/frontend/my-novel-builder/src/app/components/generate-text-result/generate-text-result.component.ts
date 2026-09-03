import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { GenerateTextRequestDto } from '../../types/dtos/generate/generate-text-request.dto';
import { GenerateTextService } from '../../services/generate-text.service';
import {
  DialogService,
  DynamicDialogConfig,
  DynamicDialogRef,
} from 'primeng/dynamicdialog';
import { ButtonModule } from 'primeng/button';
import { Subscription } from 'rxjs';
import { GenerateTextPreviewDialogService } from '../generate-text-preview/generate-text-preview-dialog.service';

export interface GenerateTextResultComponentData {
  textToReplace: string; // In HTML format
  request: GenerateTextRequestDto;
}

@Component({
  selector: 'app-generate-text-result',
  standalone: true,
  imports: [ButtonModule],
  providers: [DialogService, GenerateTextPreviewDialogService],
  templateUrl: './generate-text-result.component.html',
  styleUrl: './generate-text-result.component.scss',
})
export class GenerateTextResultComponent implements OnInit, OnDestroy {
  private generationTimerId: ReturnType<typeof setInterval> | null = null;
  private generationStartedAt: number | null = null;
  private generationSubscription: Subscription | null = null;
  private previewDialogRef: DynamicDialogRef | null = null;

  config = inject(DynamicDialogConfig);
  dialogRef = inject(DynamicDialogRef);

  data!: GenerateTextResultComponentData;

  readonly generateTextService: GenerateTextService =
    inject(GenerateTextService);
  private readonly previewDialogService = inject(
    GenerateTextPreviewDialogService,
  );
  isGenerating = true;
  hasGenerationError = false;
  generatedText = '';
  generationElapsedSeconds = 0;
  lastGenerationDurationSeconds: number | null = null;

  constructor() {
    this.data = this.config.data as GenerateTextResultComponentData;
  }

  ngOnInit(): void {
    this.generateText();
  }

  ngOnDestroy(): void {
    this.generationSubscription?.unsubscribe();
    this.generationSubscription = null;
    this.stopGenerationTimer();
    this.previewDialogRef?.close();
  }

  previewPrompt(): void {
    if (this.isGenerating) {
      return;
    }

    this.previewDialogRef = this.previewDialogService.open(this.data.request);
  }

  generateText(): void {
    this.generationSubscription?.unsubscribe();
    this.stopGenerationTimer();
    this.generatedText = '[Generating text...]';
    this.isGenerating = true;
    this.hasGenerationError = false;
    this.lastGenerationDurationSeconds = null;
    this.startGenerationTimer();

    this.generationSubscription = this.generateTextService
      .generateText(this.data.request)
      .subscribe({
        next: (update) => {
          if (update.content.length > 0) {
            this.generatedText = update.content;
          }

          if (update.isComplete) {
            this.isGenerating = false;
            this.stopGenerationTimer();
          }
        },
        error: (error) => {
          console.error('Error generating text:', error);
          this.isGenerating = false;
          this.hasGenerationError = true;
          this.stopGenerationTimer();
        },
        complete: () => {
          if (this.isGenerating) {
            this.isGenerating = false;
            this.stopGenerationTimer();
          }
        },
      });
  }

  get retryButtonLabel(): string {
    return this.isGenerating
      ? `Generating (${this.generationElapsedSeconds}s)`
      : 'Retry';
  }

  get generationStatusLabel(): string | null {
    if (this.isGenerating) {
      return null;
    }

    if (this.lastGenerationDurationSeconds === null) {
      return null;
    }

    return `Generation took ${this.lastGenerationDurationSeconds}s`;
  }

  accept(): void {
    if (this.isGenerating || this.hasGenerationError) {
      return;
    }

    // Replace multiple linebreaks with a single linebreak
    // TODO: Make this configurable, this is just my personal preference
    this.dialogRef.close(this.generatedText.replace(/\n{2,}/g, '\n'));
  }

  discard(): void {
    this.dialogRef.close();
  }

  goBack(): void {
    this.dialogRef.close('back');
  }

  private startGenerationTimer(): void {
    this.stopGenerationTimer();
    this.generationStartedAt = Date.now();
    this.generationElapsedSeconds = 0;
    this.generationTimerId = setInterval(() => {
      if (this.generationStartedAt === null) {
        return;
      }

      this.generationElapsedSeconds = Math.floor(
        (Date.now() - this.generationStartedAt) / 1000,
      );
    }, 1000);
  }

  private stopGenerationTimer(): void {
    if (this.generationTimerId !== null) {
      clearInterval(this.generationTimerId);
      this.generationTimerId = null;
    }

    if (this.generationStartedAt !== null) {
      this.lastGenerationDurationSeconds = Math.floor(
        (Date.now() - this.generationStartedAt) / 1000,
      );
      this.generationElapsedSeconds = this.lastGenerationDurationSeconds;
      this.generationStartedAt = null;
    }
  }
}
