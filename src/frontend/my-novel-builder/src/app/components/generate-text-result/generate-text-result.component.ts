import { Component, Inject, OnInit, inject } from '@angular/core';
import { GenerateTextRequestDto } from '../../types/dtos/generate/generate-text-request.dto';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import {
  HttpEvent,
  HttpEventType,
  HttpDownloadProgressEvent,
  HttpResponse,
} from '@angular/common/http';
import { GenerateService } from '../../services/generate.service';
import { GenerateTextResponseChunkDto } from '../../types/dtos/generate/generate-text-response-chunk.dto';
import { ToastrService } from 'ngx-toastr';

export interface GenerateTextResultComponentData {
  textToReplace: string; // In HTML format
  request: GenerateTextRequestDto;
}

@Component({
  selector: 'app-generate-text-result',
  standalone: true,
  imports: [],
  templateUrl: './generate-text-result.component.html',
  styleUrl: './generate-text-result.component.scss',
})
export class GenerateTextResultComponent implements OnInit {
  readonly generateService: GenerateService = inject(GenerateService);
  readonly toastr: ToastrService = inject(ToastrService);
  isGenerating = true;
  generatedText = '';

  constructor(
    @Inject(MAT_DIALOG_DATA)
    public data: GenerateTextResultComponentData,
    public dialogRef: MatDialogRef<GenerateTextResultComponent>
  ) {}

  ngOnInit(): void {
    this.generateText();
  }

  generateText(): void {
    this.generatedText = '[Generating text...]';
    this.isGenerating = true;

    this.generateService.generateText(this.data.request).subscribe({
      next: (event: HttpEvent<string>) => {
        if (event.type === HttpEventType.DownloadProgress) {
          const response = (event as HttpDownloadProgressEvent)
            .partialText as string;
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
            this.isGenerating = false;
          }
        }
      },
      error: (error) => {
        this.toastr.error(
          error.message,
          'An error occurred while generating the text'
        );
        this.isGenerating = false;
      },
    });
  }

  accept() {
    // Replace multiple linebreaks with a single linebreak
    // TODO: Make this configurable, this is just my personal preference
    this.dialogRef.close(this.generatedText.replace(/\n{2,}/g, '\n'));
  }

  discard() {
    this.dialogRef.close();
  }
}
