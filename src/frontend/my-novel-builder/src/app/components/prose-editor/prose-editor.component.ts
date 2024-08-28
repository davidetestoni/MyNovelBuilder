import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import {
  Prose,
  TextSectionItem,
  SectionItem,
  SectionItemType,
  ImageSectionItem,
  Section,
} from '../../types/dtos/novel/prose';
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
import { GenerateService } from '../../services/generate.service';
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
import { GenerateTextRequestDto } from '../../types/dtos/generate/generate-text-request.dto';
import {
  GenerateTextResultComponent,
  GenerateTextResultComponentData,
} from '../generate-text-result/generate-text-result.component';
import Quill from 'quill';

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
  readonly dialog = inject(MatDialog);
  readonly toastr: ToastrService = inject(ToastrService);
  readonly generateService: GenerateService = inject(GenerateService);
  showEditorControls = false;
  editorControlsPosition: { x: number; y: number } = { x: 0, y: 0 };
  lastSelection: LastSelection | null = null;

  isTextSectionItem(item: SectionItem): item is TextSectionItem {
    return item.$type === SectionItemType.Text;
  }

  isImageSectionItem(item: SectionItem): item is ImageSectionItem {
    return item.$type === SectionItemType.Image;
  }

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
      items: [
        <TextSectionItem>{
          $type: SectionItemType.Text,
          text: '',
        },
      ],
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

  addTextSectionItem(chapterIndex: number, sectionIndex: number) {
    this.prose.chapters[chapterIndex].sections[sectionIndex].items =
      this.prose.chapters[chapterIndex].sections[sectionIndex].items.concat(<
        TextSectionItem
      >{
        $type: SectionItemType.Text,
        text: '',
      });
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

  updateTextSectionItem(item: TextSectionItem, event: Blur) {
    item.text = event.editor.getSemanticHTML();
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

  openGenerateSectionSummaryDialog(chapterIndex: number, sectionIndex: number) {
    const prompts = this.prompts.filter(
      (p) => p.type === PromptType.SummarizeText
    );

    if (prompts.length === 0) {
      this.toastr.error('No summarization prompts available');
      return;
    }

    const dialogRef = this.dialog.open(GenerateTextComponent, {
      minWidth: '50vw',
      data: <GenerateTextComponentData>{
        prompts: prompts,
        instructions: null,
        instructionsRequired: false,
        context: this.prose.chapters[chapterIndex].sections[sectionIndex].items
          .filter(this.isTextSectionItem)
          .map((item) => this.getRawText(item.text))
          .join(''),
        novelId: this.novelId,
      },
    });

    dialogRef.afterClosed().subscribe((request: GenerateTextRequestDto) => {
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

    this.generateService.generateText(request).subscribe({
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
      (p) =>
        p.type === PromptType.GenerateText ||
        p.type === PromptType.ReplaceTextGuided
    );

    if (prompts.length === 0) {
      this.toastr.error('No summarization prompts available');
      return;
    }

    const dialogRef = this.dialog.open(GenerateTextComponent, {
      minWidth: '50vw',
      data: <GenerateTextComponentData>{
        prompts: prompts,
        instructions: null,
        instructionsRequired: true, // This should be defined by the prompt
        context: this.assembleGenerateTextContext(),
        novelId: this.novelId,
      },
    });

    dialogRef.afterClosed().subscribe((request: GenerateTextRequestDto) => {
      if (request) {
        this.openGenerateTextResultDialog(request);
      }
    });
  }

  openGenerateTextResultDialog(request: GenerateTextRequestDto) {
    const dialogRef = this.dialog.open(GenerateTextResultComponent, {
      minWidth: '50vw',
      data: <GenerateTextResultComponentData>{
        request: request,
        textToReplace: this.lastSelection?.text ?? '',
      },
    });

    dialogRef.afterClosed().subscribe((generatedText: string) => {
      if (generatedText) {
        // If the range has 0 length, append the generated text
        // at the end of the range in the Quill editor. Otherwise,
        // replace the selected text with the generated text.
        if (this.lastSelection!.range.length === 0) {
          this.lastSelection!.editor.insertText(
            this.lastSelection!.range.index,
            generatedText
          );
        } else {
          this.lastSelection!.editor.deleteText(
            this.lastSelection!.range.index,
            this.lastSelection!.range.length
          );
          this.lastSelection!.editor.insertText(
            this.lastSelection!.range.index,
            generatedText
          );
        }
      }
    });
  }

  assembleGenerateTextContext(): string {
    if (!this.lastSelection) {
      return 'There is no text selected';
    }

    // If the last selection has some text, just provide
    // the text as context
    if (this.lastSelection.text.length > 0) {
      return this.lastSelection.text;
    }

    const chapter = this.prose.chapters[this.lastSelection.chapterIndex];

    // Context = summary of the last 5 sections BEFORE the current section
    // + the text of the current section up to before the selected text.
    const sections = chapter.sections.slice(
      Math.max(0, this.lastSelection.sectionIndex - 5),
      this.lastSelection.sectionIndex
    );

    const context = sections
      .map((section) => section.summary)
      .join('\n\n')
      .concat('\n\n')
      .concat(
        chapter.sections[this.lastSelection.sectionIndex].items
          .filter(this.isTextSectionItem)
          .map((item) => this.getRawText(item.text))
          .join('\n\n')
          .slice(0, this.lastSelection.range.index)
      );

    return context;
  }
}
