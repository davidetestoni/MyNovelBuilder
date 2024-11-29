import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { Prose, Section } from '../../types/dtos/novel/prose';
import { CommonModule } from '@angular/common';
import {
  Blur,
  EditorChangeContent,
  EditorChangeSelection,
  QuillModule,
  Range,
} from 'ngx-quill';
import { environment } from '../../../environment';
import { FormsModule } from '@angular/forms';
import { ToastrModule, ToastrService } from 'ngx-toastr';
import { GenerateTextService } from '../../services/generate-text.service';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { PromptType } from '../../types/enums/prompt-type';
import {
  HttpDownloadProgressEvent,
  HttpEvent,
  HttpEventType,
  HttpResponse,
} from '@angular/common/http';
import { GenerateTextResponseChunkDto } from '../../types/dtos/generate/generate-text-response-chunk.dto';
import { MatDialog } from '@angular/material/dialog';
import {
  GenerateTextComponent,
  GenerateTextComponentData,
} from '../generate-text/generate-text.component';
import {
  CreateCompendiumRecordContextInfoDto,
  GenerateTextContextInfoDto,
  GenerateTextRequestDto,
  ReplaceTextContextInfoDto,
  SummarizeTextContextInfoDto,
  TextGenerationType,
} from '../../types/dtos/generate/generate-text-request.dto';
import {
  GenerateTextResultComponent,
  GenerateTextResultComponentData,
} from '../generate-text-result/generate-text-result.component';
import Quill from 'quill';
import { GenerateAudioService } from '../../services/generate-audio.service';
import { GenerateCompendiumRecordComponentData, GenerateCompendiumRecordResultComponent } from '../generate-compendium-record-result/generate-compendium-record-result.component';

interface LastSelection {
  editor: Quill;
  range: Range;
  text: string;
  chapterIndex: number;
  sectionIndex: number;
}

@Component({
  selector: 'app-prose-editor',
  standalone: true,
  imports: [CommonModule, FormsModule, QuillModule, ToastrModule],
  templateUrl: './prose-editor.component.html',
  styleUrl: './prose-editor.component.scss',
})
export class ProseEditorComponent {
  @Input() novelId!: string;
  @Input() prose!: Prose;
  @Input() prompts!: PromptDto[];
  @Output() proseChange: EventEmitter<Prose> = new EventEmitter<Prose>();
  @Output() recordsChange: EventEmitter<void> = new EventEmitter<void>();
  readonly dialog = inject(MatDialog);
  readonly toastr: ToastrService = inject(ToastrService);
  readonly generateTextService: GenerateTextService = inject(GenerateTextService);
  readonly generateAudioService: GenerateAudioService = inject(GenerateAudioService);
  showEditorControls = false;
  editorControlsPosition: { x: number; y: number } = { x: 0, y: 0 };
  lastSelection: LastSelection | null = null;

  getImageUrl(imageId: string): string {
    // TODO: This should come directly from the API in ImageSectionItem
    // instead of being built in the client.
    return `${environment.api.staticFilesUrl}/novels/${this.novelId}/images/${imageId}`;
  }

  addChapter() {
    this.prose.chapters = this.prose.chapters.concat({
      title: `Chapter ${this.prose.chapters.length + 1}`,
      sections: [],
    });
    this.saveProse();
  }

  removeChapter(chapterIndex: number) {
    // To remove a chapter, it must be empty to avoid user data loss
    if (this.prose.chapters[chapterIndex].sections.length > 0) {
      this.toastr.error(
        'Cannot remove a chapter that is not empty. Please remove all sections first.'
      );
      return;
    }

    this.prose.chapters = this.prose.chapters.filter(
      (_, index) => index !== chapterIndex
    );
    this.saveProse();
  }

  addSection(chapterIndex: number) {
    // For now:
    // - a section is always added at the end of the chapter
    // - a section is only made up of a single text item, since the
    //   WYSIWYG editor can handle images on its own without the need
    //   for a separate section type
    this.prose.chapters[chapterIndex].sections = this.prose.chapters[
      chapterIndex
    ].sections.concat({
      summary: '[Missing summary]',
      text: '',
    });
    this.saveProse();
  }

  removeSection(chapterIndex: number, sectionIndex: number) {
    // Ask for confirmation before removing a section
    // TODO: This should be a material modal dialog instead of a browser dialog
    if (
      !confirm(
        'Are you sure you want to remove this section? This action cannot be undone.'
      )
    ) {
      return;
    }

    this.prose.chapters[chapterIndex].sections = this.prose.chapters[
      chapterIndex
    ].sections.filter((_, index) => index !== sectionIndex);
    this.saveProse();
  }

  updateChapterTitle(chapterIndex: number, event: Event) {
    const elem = event.target as HTMLInputElement;

    // If the user tries to remove the title, restore it
    if (elem.innerText.trim() === '') {
      elem.innerText = this.prose.chapters[chapterIndex].title;
      return;
    }

    this.prose.chapters[chapterIndex].title = elem.innerText;
    this.saveProse();
  }

  updateSectionText(section: Section, event: Blur) {
    section.text = event.editor.getSemanticHTML();
    this.saveProse();
  }

  updateSectionSummary(section: Section, event: Event) {
    const elem = event.target as HTMLInputElement;

    // HACK: This is an ugly workaround to avoid empty summaries, otherwise
    // the user would not be able to click on the summary to edit it.
    if (elem.innerText.trim() === '') {
      elem.innerText = '[Missing summary]';
    }

    section.summary = elem.innerText;
    this.saveProse();
  }

  saveProse() {
    // TODO: This should be debounced to avoid sending too many requests
    // TODO: Don't send the entire prose, only the changed parts
    this.proseChange.emit(this.prose);
  }

  preventReturnKey(event: KeyboardEvent) {
    if (event.key === 'Enter') {
      event.preventDefault();
    }
  }

  editorInit(quill: any) {
    // This clears the background and text color when pasting text
    quill.clipboard.addMatcher(
      Node.ELEMENT_NODE,
      function (node: any, delta: any) {
        delta.forEach((e: any) => {
          if (e.attributes) {
            e.attributes.color = '';
            e.attributes.background = '';
          }
        });
        return delta;
      }
    );
  }

  editorChange(
    event: EditorChangeContent | EditorChangeSelection,
    chapterIndex: number,
    sectionIndex: number
  ) {
    if (event.event !== 'selection-change') {
      return;
    }

    const proseEditorBoundingBox = document
      .querySelector('#prose-editor')!
      .getBoundingClientRect();

    const quillEditorBoundingBox =
      event.editor.container.getBoundingClientRect();

    const range = event.range;

    if (range === null) {
      this.showEditorControls = false;
      return;
    }

    const lastCharRange = {
      index: range.index + (range.length > 0 ? range.length - 1 : 0),
      length: 1,
    };

    const rangeBounds = event.editor.getBounds(lastCharRange)!;

    this.editorControlsPosition = {
      x:
        quillEditorBoundingBox.left -
        proseEditorBoundingBox.left +
        rangeBounds.right +
        10,
      y:
        quillEditorBoundingBox.top -
        proseEditorBoundingBox.top +
        rangeBounds.bottom -
        10,
    };

    this.showEditorControls = true;

    this.lastSelection = {
      editor: event.editor,
      range: range,
      chapterIndex: chapterIndex,
      sectionIndex: sectionIndex,
      text: range.length > 0 ? event.editor.getText(range) : '',
    };
  }

  private getRawText(html: string): string {
    const div = document.createElement('div');
    div.innerHTML = html;
    return div.innerText;
  }

  textToSpeech(chapterIndex: number, sectionIndex: number) {
    this.generateAudioService.textToSpeech({
      modelId: null,
      voiceId: "233", // TODO: Read this from the user's settings
      message: this.getRawText(this.prose.chapters[chapterIndex].sections[sectionIndex].text),
    }).subscribe((event: HttpEvent<Blob>) => {
      if (event.type === HttpEventType.Response) {
        if (event.body !== null) {
          const audio = new Audio();
          audio.src = URL.createObjectURL(event.body);
          audio.play();
        }
      }
    });
  }

  openGenerateSectionSummaryDialog(chapterIndex: number, sectionIndex: number) {
    const prompts = this.prompts.filter(
      (p) => p.type === PromptType.SummarizeText
    );

    if (prompts.length === 0) {
      this.toastr.error('No summarization prompts available');
      return;
    }

    this.dialog.open(GenerateTextComponent, {
      minWidth: '50vw',
      data: <GenerateTextComponentData>{
        prompts: prompts,
        instructionsRequired: false,
        contextInfo: <SummarizeTextContextInfoDto>{
          $type: TextGenerationType.SummarizeText,
          chapterIndex: chapterIndex,
          sectionIndex: sectionIndex,
        },
        novelId: this.novelId,
      },
    }).afterClosed().subscribe((request: GenerateTextRequestDto) => {
      if (request) {
        this.generateSectionSummary(chapterIndex, sectionIndex, request);
      }
    });
  }

  generateSectionSummary(
    chapterIndex: number,
    sectionIndex: number,
    request: GenerateTextRequestDto
  ) {
    // Clear the current summary
    this.prose.chapters[chapterIndex].sections[sectionIndex].summary =
      '[Summarizing...]';

    this.generateTextService.generateText(request).subscribe({
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

            this.prose.chapters[chapterIndex].sections[sectionIndex].summary =
              message;
          }
        } else if (event.type === HttpEventType.Response) {
          const response = event as HttpResponse<string>;
          const responseChunks = response
            .body!.split('\n')
            .filter((item) => item.length > 0)
            .map((item) => JSON.parse(item) as GenerateTextResponseChunkDto);

          if (responseChunks.length > 0) {
            const message = responseChunks.map((item) => item.content).join('');

            this.prose.chapters[chapterIndex].sections[sectionIndex].summary =
              message;

            this.saveProse();
          }
        }
      },
    });
  }

  openGenerateTextDialog() {
    const prompts = this.prompts.filter(
      (p) => p.type === PromptType.GenerateText
    );

    if (prompts.length === 0) {
      this.toastr.error('No generation prompts available');
      return;
    }

    // Save the prose to avoid losing the user's changes since
    // all generation happens on the backend with the saved prose
    this.saveProse();

    this.dialog.open(GenerateTextComponent, {
      minWidth: '50vw',
      data: <GenerateTextComponentData>{
        prompts: prompts,
        contextInfo: <GenerateTextContextInfoDto>{
          $type: TextGenerationType.GenerateText,
          chapterIndex: this.lastSelection!.chapterIndex,
          sectionIndex: this.lastSelection!.sectionIndex,
          textOffset: this.lastSelection!.range.index,
          instructions: null,
        },
        instructionsRequired: true, // This should be defined by the prompt
        novelId: this.novelId,
      },
    }).afterClosed().subscribe((request: GenerateTextRequestDto) => {
      if (request) {
        this.openGenerateTextResultDialog(request);
      }
    });
  }

  openGenerateTextResultDialog(request: GenerateTextRequestDto) {
    this.dialog.open(GenerateTextResultComponent, {
      minWidth: '50vw',
      data: <GenerateTextResultComponentData>{
        request: request,
        textToReplace: this.lastSelection?.text ?? '',
      },
    }).afterClosed().subscribe((generatedText: string) => {
      if (generatedText) {
        const contextInfo = request.contextInfo as GenerateTextContextInfoDto;

        // Append the generated text at the end of the range in the Quill editor.
        this.lastSelection!.editor.insertText(
          contextInfo.textOffset,
          generatedText
        );
      }
    });
  }

  openReplaceTextDialog() {
    const prompts = this.prompts.filter(
      (p) => p.type === PromptType.ReplaceText
    );

    if (prompts.length === 0) {
      this.toastr.error('No replacement prompts available');
      return;
    }

    // Save the prose to avoid losing the user's changes since
    // all generation happens on the backend with the saved prose
    this.saveProse();

    this.dialog.open(GenerateTextComponent, {
      minWidth: '50vw',
      data: <GenerateTextComponentData>{
        prompts: prompts,
        contextInfo: <ReplaceTextContextInfoDto>{
          $type: TextGenerationType.ReplaceText,
          chapterIndex: this.lastSelection!.chapterIndex,
          sectionIndex: this.lastSelection!.sectionIndex,
          textOffset: this.lastSelection!.range.index,
          textLength: this.lastSelection!.range.length,
          instructions: null,
        },
        instructionsRequired: true, // This should be defined by the prompt
        novelId: this.novelId,
      },
    }).afterClosed().subscribe((request: GenerateTextRequestDto) => {
      if (request) {
        this.openReplaceTextResultDialog(request);
      }
    });
  }

  openReplaceTextResultDialog(request: GenerateTextRequestDto) {
    this.dialog.open(GenerateTextResultComponent, {
      minWidth: '50vw',
      data: <GenerateTextResultComponentData>{
        request: request,
        textToReplace: this.lastSelection?.text ?? '',
      },
    }).afterClosed().subscribe((generatedText: string) => {
      if (generatedText) {
        const contextInfo = request.contextInfo as ReplaceTextContextInfoDto;

        // Replace the selected text with the generated text.
        // Do not use the current selection's range, as it may have changed
        // since the dialog was opened.
        this.lastSelection!.editor.deleteText(
          contextInfo.textOffset,
          contextInfo.textLength
        );
        this.lastSelection!.editor.insertText(
          contextInfo.textOffset,
          generatedText
        );
      }
    });
  }

  openCreateCompendiumRecordDialog() {
    const prompts = this.prompts.filter(
      (p) => p.type === PromptType.CreateCompendiumRecord
    );

    if (prompts.length === 0) {
      this.toastr.error('No compendium record prompts available');
      return;
    }

    this.dialog.open(GenerateTextComponent, {
      minWidth: '50vw',
      data: <GenerateTextComponentData>{
        prompts: prompts,
        contextInfo: <CreateCompendiumRecordContextInfoDto>{
          $type: TextGenerationType.CreateCompendiumRecord,
          chapterIndex: this.lastSelection!.chapterIndex,
          sectionIndex: this.lastSelection!.sectionIndex,
          textOffset: this.lastSelection!.range.index,
          textLength: this.lastSelection!.range.length,
          instructions: null,
        },
        instructionsRequired: true,
        novelId: this.novelId,
      },
    }).afterClosed().subscribe((request: GenerateTextRequestDto) => {
      if (request) {
        this.dialog.open(GenerateTextResultComponent, {
          minWidth: '50vw',
          data: <GenerateTextResultComponentData>{
            request: request,
            textToReplace: ''
          },
        }).afterClosed().subscribe((generatedText: string) => {
          if (generatedText) {
            this.dialog.open(GenerateCompendiumRecordResultComponent, {
              minWidth: '50vw',
              data: <GenerateCompendiumRecordComponentData>{
                generatedText: generatedText,
                novelId: this.novelId,
              },
            }).afterClosed().subscribe((changed) => {
              if (changed === true) {
                this.recordsChange.emit();
              }
            });
          }
        });
      }
    });
  }
}
