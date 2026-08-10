import { Component, ElementRef, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { ButtonModule } from 'primeng/button';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { TextareaModule } from 'primeng/textarea';
import { finalize } from 'rxjs';
import { NovelService } from '../../services/novel.service';

export interface ImportMarkdownDialogData {
  novelId: string;
}

export interface ImportMarkdownDialogResult {
  novelId: string;
}

@Component({
  selector: 'app-import-markdown-dialog',
  standalone: true,
  imports: [FormsModule, ButtonModule, TextareaModule],
  templateUrl: './import-markdown-dialog.component.html',
  styleUrl: './import-markdown-dialog.component.scss',
})
export class ImportMarkdownDialogComponent {
  @ViewChild('markdownFileInput')
  private markdownFileInput?: ElementRef<HTMLInputElement>;

  private readonly dialogRef = inject(DynamicDialogRef);
  private readonly config = inject(DynamicDialogConfig);
  private readonly novelService = inject(NovelService);
  private readonly toastr = inject(ToastrService);

  readonly data = (this.config.data ?? {}) as ImportMarkdownDialogData;
  readonly markdownPlaceholder = `# The Clockmaker's Map
by Mira Vale

A missing atlas leads Elian into a city that should not exist.

## Chapter 1 — The Brass Door
Rain silvered the station roof.

Elian opened the letter. **The map inside was warm.**

## Chapter 2 — The Unmapped City
The final train arrived without a driver.`;
  selectedFile: File | null = null;
  pastedMarkdown = '';
  isImporting = false;

  get hasMarkdownSource(): boolean {
    return this.selectedFile !== null || this.pastedMarkdown.trim() !== '';
  }

  onFileSelected(event: Event): void {
    const input = event.target;
    if (!(input instanceof HTMLInputElement)) {
      return;
    }

    const file = input.files?.[0] ?? null;
    if (file === null) {
      this.selectedFile = null;
      return;
    }

    if (!/\.(md|markdown)$/i.test(file.name)) {
      this.selectedFile = null;
      input.value = '';
      this.toastr.error('Choose a Markdown file ending in .md or .markdown.');
      return;
    }

    if (file.size > 5 * 1024 * 1024) {
      this.selectedFile = null;
      input.value = '';
      this.toastr.error('The Markdown file cannot exceed 5 MB.');
      return;
    }

    this.selectedFile = file;
    this.pastedMarkdown = '';
  }

  onPastedMarkdownChange(markdown: string): void {
    this.pastedMarkdown = markdown;
    if (markdown.trim() === '') {
      return;
    }

    this.selectedFile = null;
    if (this.markdownFileInput) {
      this.markdownFileInput.nativeElement.value = '';
    }
  }

  importMarkdown(): void {
    if (!this.hasMarkdownSource || this.isImporting) {
      return;
    }

    const file =
      this.selectedFile ??
      new File([this.pastedMarkdown], 'pasted-markdown.md', {
        type: 'text/markdown',
      });
    if (file.size > 5 * 1024 * 1024) {
      this.toastr.error('The Markdown content cannot exceed 5 MB.');
      return;
    }

    this.isImporting = true;

    this.novelService
      .replaceNovelProseFromMarkdown(this.data.novelId, file)
      .pipe(finalize(() => (this.isImporting = false)))
      .subscribe({
        next: () => {
          this.toastr.success('Novel prose replaced successfully.');
          this.dialogRef.close({
            novelId: this.data.novelId,
          } satisfies ImportMarkdownDialogResult);
        },
        error: () => {
          this.toastr.error('Failed to import the Markdown file.');
        },
      });
  }
}
