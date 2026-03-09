import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { GenerateTextRequestDto } from '../../types/dtos/generate/generate-text-request.dto';
import {
  HttpEvent,
  HttpEventType,
  HttpDownloadProgressEvent,
  HttpResponse,
} from '@angular/common/http';
import { GenerateTextService } from '../../services/generate-text.service';
import { GenerateTextResponseChunkDto } from '../../types/dtos/generate/generate-text-response-chunk.dto';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { ButtonModule } from 'primeng/button';

export interface GenerateTextResultComponentData {
  textToReplace: string; // In HTML format
  request: GenerateTextRequestDto;
}

@Component({
  selector: 'app-generate-text-result',
  standalone: true,
  imports: [ButtonModule],
  templateUrl: './generate-text-result.component.html',
  styleUrl: './generate-text-result.component.scss',
})
export class GenerateTextResultComponent implements OnInit, OnDestroy {
  private generationTimerId: ReturnType<typeof setInterval> | null = null;
  private generationStartedAt: number | null = null;

  config = inject(DynamicDialogConfig);
  dialogRef = inject(DynamicDialogRef);

  data!: GenerateTextResultComponentData;

  readonly generateTextService: GenerateTextService =
    inject(GenerateTextService);
  isGenerating = true;
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
    this.stopGenerationTimer();
  }

  generateText(): void {
    this.generatedText = '[Generating text...]';
    this.isGenerating = true;
    this.lastGenerationDurationSeconds = null;
    this.startGenerationTimer();

    this.generateTextService.generateText(this.data.request).subscribe({
      next: (event: HttpEvent<string>) => {
        if (event.type === HttpEventType.DownloadProgress) {
          const response = (event as HttpDownloadProgressEvent)
            .partialText as string;
          if (response === undefined) {
            return;
          }
          const responseChunks = response
            .split('\n')
            .filter((item) => item.length > 0)
            .map((item) => JSON.parse(item) as GenerateTextResponseChunkDto);
          if (responseChunks.length > 0) {
            const message = responseChunks.map((item) => item.content).join('');

            this.generatedText = message;
          }
        } else if (event.type === HttpEventType.Response) {
          const response = event as HttpResponse<string>;
          const responseChunks = response
            .body!.split('\n')
            .filter((item) => item.length > 0)
            .map((item) => JSON.parse(item) as GenerateTextResponseChunkDto);

          if (responseChunks.length > 0) {
            const message = responseChunks.map((item) => item.content).join('');

            this.generatedText = message;
          }

          this.isGenerating = false;
          this.stopGenerationTimer();
        }
      },
      error: (error) => {
        console.error('Error generating text:', error);
        this.isGenerating = false;
        this.stopGenerationTimer();
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

  accept() {
    // Replace multiple linebreaks with a single linebreak
    // TODO: Make this configurable, this is just my personal preference
    this.dialogRef.close(this.generatedText.replace(/\n{2,}/g, '\n'));
  }

  discard() {
    this.dialogRef.close();
  }

  goBack() {
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
