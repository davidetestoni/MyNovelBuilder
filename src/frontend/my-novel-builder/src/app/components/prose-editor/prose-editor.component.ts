import {
  Component,
  EventEmitter,
  Input,
  OnDestroy,
  Output,
  inject,
} from '@angular/core';
import { Prose, Section, RecordOverride } from '../../types/dtos/novel/prose';
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
  NovelTextGenerationType,
} from '../../types/dtos/generate/generate-text-request.dto';
import {
  GenerateTextResultComponent,
  GenerateTextResultComponentData,
} from '../generate-text-result/generate-text-result.component';
import Quill from 'quill';
import { GenerateAudioService } from '../../services/generate-audio.service';
import {
  GenerateCompendiumRecordComponentData,
  GenerateCompendiumRecordResultComponent,
} from '../generate-compendium-record-result/generate-compendium-record-result.component';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { TooltipModule } from 'primeng/tooltip';
import { NovelService } from '../../services/novel.service';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { ImageSourceSelectorComponent } from '../image-source-selector/image-source-selector.component';
import { GenerateImageComponent } from '../generate-image/generate-image.component';
import { StreamingWavPlayer } from '../../utils/streaming-wav-player';
import { readImageFileFromClipboard } from '../../utils/clipboard-image';

interface LastSelection {
  editor: Quill;
  range: Range;
  text: string;
  chapterIndex: number;
  sectionIndex: number;
}

import { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import {
  RecordOverridesEditorComponent,
  RecordOverridesEditorComponentData,
} from '../record-overrides-editor/record-overrides-editor.component';

@Component({
  selector: 'app-prose-editor',
  standalone: true,
  templateUrl: './prose-editor.component.html',
  styleUrl: './prose-editor.component.scss',
  imports: [
    CommonModule,
    FormsModule,
    QuillModule,
    ToastrModule,
    TooltipModule,
    ConfirmDialogModule,
  ],
  providers: [DialogService, ConfirmationService],
})
export class ProseEditorComponent implements OnDestroy {
  @Input() novelId!: string;
  @Input() prose!: Prose;
  @Input() selectedChapterIndex: number | null = null;
  @Input() prompts!: PromptDto[];
  @Input() compendia: CompendiumDto[] | null = null;
  @Output() proseChange: EventEmitter<Prose> = new EventEmitter<Prose>();
  @Output() recordsChange: EventEmitter<void> = new EventEmitter<void>();
  @Output() proseImageClicked: EventEmitter<string> =
    new EventEmitter<string>();
  private dialogService = inject(DialogService);
  private confirmationService = inject(ConfirmationService);
  readonly toastr: ToastrService = inject(ToastrService);
  readonly generateTextService: GenerateTextService =
    inject(GenerateTextService);
  readonly generateAudioService: GenerateAudioService =
    inject(GenerateAudioService);
  readonly novelService = inject(NovelService);
  showEditorControls = false;
  editorControlsPosition: { x: number; y: number } = { x: 0, y: 0 };
  lastSelection: LastSelection | null = null;
  private readonly averageReadingWpm = 238;

  private dialogRef: DynamicDialogRef | null = null;

  ngOnDestroy(): void {
    if (this.dialogRef) {
      this.dialogRef.close();
    }
  }

  getImageUrl(fileName: string): string {
    // TODO: This should come directly from the API in ImageSectionItem
    // instead of being built in the client.
    return `${environment.api.staticFilesUrl}/novels/${this.novelId}/prose-images/${fileName}`;
  }

  addChapter() {
    this.prose.chapters = this.prose.chapters.concat({
      title: `Chapter ${this.prose.chapters.length + 1}`,
      sections: [],
      storyEvents: [],
    });
    this.saveProse();
  }

  removeChapter(chapterIndex: number) {
    // To remove a chapter, it must be empty to avoid user data loss
    if (this.prose.chapters[chapterIndex].sections.length > 0) {
      this.toastr.error(
        'Cannot remove a chapter that is not empty. Please remove all sections first.',
      );
      return;
    }

    this.prose.chapters = this.prose.chapters.filter(
      (_, index) => index !== chapterIndex,
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
      images: [],
      recordOverrides: [],
    });
    this.saveProse();
  }

  removeSection(chapterIndex: number, sectionIndex: number) {
    this.confirmationService.confirm({
      message:
        'Are you sure you want to remove this section? This action cannot be undone.',
      header: 'Confirm Section Removal',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.prose.chapters[chapterIndex].sections = this.prose.chapters[
          chapterIndex
        ].sections.filter((_, index) => index !== sectionIndex);
        this.saveProse();
      },
    });
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

  getChapterWordCount(chapter: Prose['chapters'][number]): number {
    const text = chapter.sections
      .map((section) => this.stripHtml(section.text))
      .join(' ')
      .trim();

    if (!text) {
      return 0;
    }

    return text.split(/\s+/).length;
  }

  getReadingTimeMinutes(wordCount: number): number {
    if (wordCount === 0) {
      return 0;
    }

    return Math.ceil(wordCount / this.averageReadingWpm);
  }

  private stripHtml(value: string): string {
    const parser = new DOMParser();
    const doc = parser.parseFromString(value, 'text/html');
    return (doc.body.textContent || '').replace(/\s+/g, ' ').trim();
  }

  private requireLastSelection(): LastSelection | null {
    if (this.lastSelection) {
      return this.lastSelection;
    }

    this.toastr.error('Please select text before using this action.');
    return null;
  }

  private parseResponseChunks(response: string | null | undefined) {
    if (!response) {
      return [] as GenerateTextResponseChunkDto[];
    }

    return response
      .split('\n')
      .filter((item) => item.length > 0)
      .map((item) => JSON.parse(item) as GenerateTextResponseChunkDto);
  }

  preventReturnKey(event: KeyboardEvent) {
    if (event.key === 'Enter') {
      event.preventDefault();
    }
  }

  editorInit(quill: Quill) {
    // This clears the background and text color when pasting text
    quill.clipboard.addMatcher(
      Node.ELEMENT_NODE,
      (_node, delta) => {
        delta.forEach((e: { attributes?: { color?: string; background?: string } }) => {
          if (e.attributes) {
            e.attributes.color = '';
            e.attributes.background = '';
          }
        });
        return delta;
      },
    );
  }

  editorChange(
    event: EditorChangeContent | EditorChangeSelection,
    chapterIndex: number,
    sectionIndex: number,
  ) {
    if (event.event !== 'selection-change') {
      return;
    }

    const proseEditor = document.querySelector('#prose-editor');
    if (!(proseEditor instanceof HTMLElement)) {
      this.showEditorControls = false;
      return;
    }

    const proseEditorBoundingBox = proseEditor.getBoundingClientRect();

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

    const rangeBounds = event.editor.getBounds(lastCharRange);
    if (!rangeBounds) {
      this.showEditorControls = false;
      return;
    }

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
    // Add space between <p> tags, otherwise it looks like
    // "First paragraph.Second paragraph."
    const normalizedHtml = html.replace(/<\/p>\s*<p>/g, '</p> <p>');
    const div = document.createElement('div');
    div.innerHTML = normalizedHtml;
    const innerText = div.innerText;
    div.remove();
    return innerText;
  }

  async textToSpeech(chapterIndex: number, sectionIndex: number) {
    const timerLabel = `TTS section ${chapterIndex}-${sectionIndex}`;
    try {
      console.time(timerLabel);
      const response =
        await this.generateAudioService.textToSpeechStreamResponse({
          message: this.getRawText(
            this.prose.chapters[chapterIndex].sections[sectionIndex].text,
          ),
        });

      const stream = response.body;
      if (!stream) {
        this.toastr.error('No audio stream was returned.');
        return;
      }
      const player = new StreamingWavPlayer();
      const reader = stream.getReader();

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        if (value) player.addChunk(value);
      }
    } catch (error) {
      console.error('WAV streaming error:', error);
    } finally {
      console.timeEnd(timerLabel);
    }
  }

  openGenerateSectionSummaryDialog(chapterIndex: number, sectionIndex: number) {
    const prompts = this.prompts.filter(
      (p) => p.type === PromptType.SummarizeText,
    );

    if (prompts.length === 0) {
      this.toastr.error('No summarization prompts available');
      return;
    }

    this.dialogRef = this.dialogService.open(GenerateTextComponent, {
      header: 'Generate Section Summary',
      width: '50vw',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      modal: true,
      closable: true,
      closeOnEscape: true,
      dismissableMask: true,
      data: <GenerateTextComponentData>{
        prompts: prompts,
        instructionsRequired: false,
        contextInfo: <SummarizeTextContextInfoDto>{
          $type: NovelTextGenerationType.SummarizeText,
          novelId: this.novelId,
          chapterIndex: chapterIndex,
          sectionIndex: sectionIndex,
        },
      },
    });

    this.dialogRef?.onClose.subscribe((request: GenerateTextRequestDto) => {
      if (request) {
        this.generateSectionSummary(chapterIndex, sectionIndex, request);
      }
    });
  }

  generateSectionSummary(
    chapterIndex: number,
    sectionIndex: number,
    request: GenerateTextRequestDto,
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
          const responseChunks = this.parseResponseChunks(response.body);

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
    const selection = this.requireLastSelection();
    if (!selection) {
      return;
    }

    const prompts = this.prompts.filter(
      (p) => p.type === PromptType.GenerateText,
    );

    if (prompts.length === 0) {
      this.toastr.error('No generation prompts available');
      return;
    }

    // Save the prose to avoid losing the user's changes since
    // all generation happens on the backend with the saved prose
    this.saveProse();

    this.dialogRef = this.dialogService.open(GenerateTextComponent, {
      header: 'Generate Text',
      width: '50vw',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      modal: true,
      closable: true,
      closeOnEscape: true,
      dismissableMask: true,
      data: <GenerateTextComponentData>{
        prompts: prompts,
        contextInfo: <GenerateTextContextInfoDto>{
          $type: NovelTextGenerationType.GenerateText,
          novelId: this.novelId,
          chapterIndex: selection.chapterIndex,
          sectionIndex: selection.sectionIndex,
          textOffset: selection.range.index,
          instructions: null,
        },
        instructionsRequired: true, // This should be defined by the prompt
      },
    });

    this.dialogRef?.onClose.subscribe((request: GenerateTextRequestDto) => {
      if (request) {
        this.openGenerateTextResultDialog(request);
      }
    });
  }

  openGenerateTextResultDialog(request: GenerateTextRequestDto) {
    this.dialogRef = this.dialogService.open(GenerateTextResultComponent, {
      header: 'Generate Text',
      width: '50vw',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      modal: true,
      closable: true,
      closeOnEscape: true,
      dismissableMask: true,
      data: <GenerateTextResultComponentData>{
        request: request,
        textToReplace: this.lastSelection?.text ?? '',
      },
    });

    this.dialogRef?.onClose.subscribe((result: string | 'back' | undefined) => {
      if (result === 'back') {
        this.openGenerateTextDialog();
      } else if (result) {
        const selection = this.lastSelection;
        if (!selection) {
          this.toastr.error('Selection is no longer available.');
          return;
        }

        const contextInfo = request.contextInfo as GenerateTextContextInfoDto;

        // Append the generated text at the end of the range in the Quill editor.
        selection.editor.insertText(contextInfo.textOffset, result);

        const section =
          this.prose.chapters[contextInfo.chapterIndex].sections[
            contextInfo.sectionIndex
          ];

        section.text = selection.editor.getSemanticHTML();
        this.saveProse();
      }
    });
  }

  openReplaceTextDialog() {
    const selection = this.requireLastSelection();
    if (!selection) {
      return;
    }

    const prompts = this.prompts.filter(
      (p) => p.type === PromptType.ReplaceText,
    );

    if (prompts.length === 0) {
      this.toastr.error('No replacement prompts available');
      return;
    }

    // Save the prose to avoid losing the user's changes since
    // all generation happens on the backend with the saved prose
    this.saveProse();

    this.dialogRef = this.dialogService.open(GenerateTextComponent, {
      header: 'Replace Text',
      width: '50vw',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      modal: true,
      closable: true,
      closeOnEscape: true,
      dismissableMask: true,
      data: <GenerateTextComponentData>{
        prompts: prompts,
        contextInfo: <ReplaceTextContextInfoDto>{
          $type: NovelTextGenerationType.ReplaceText,
          novelId: this.novelId,
          chapterIndex: selection.chapterIndex,
          sectionIndex: selection.sectionIndex,
          textOffset: selection.range.index,
          textLength: selection.range.length,
          instructions: null,
        },
        instructionsRequired: true, // This should be defined by the prompt
      },
    });

    this.dialogRef?.onClose.subscribe((request: GenerateTextRequestDto) => {
      if (request) {
        this.openReplaceTextResultDialog(request);
      }
    });
  }

  openReplaceTextResultDialog(request: GenerateTextRequestDto) {
    this.dialogRef = this.dialogService.open(GenerateTextResultComponent, {
      header: 'Replace Text',
      width: '50vw',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      modal: true,
      closable: true,
      closeOnEscape: true,
      dismissableMask: true,
      data: <GenerateTextResultComponentData>{
        request: request,
        textToReplace: this.lastSelection?.text ?? '',
      },
    });

    this.dialogRef?.onClose.subscribe((result: string | 'back' | undefined) => {
      if (result === 'back') {
        this.openReplaceTextDialog();
      } else if (result) {
        const selection = this.lastSelection;
        if (!selection) {
          this.toastr.error('Selection is no longer available.');
          return;
        }

        const contextInfo = request.contextInfo as ReplaceTextContextInfoDto;

        // Replace the selected text with the generated text.
        // Do not use the current selection's range, as it may have changed
        // since the dialog was opened.
        selection.editor.deleteText(
          contextInfo.textOffset,
          contextInfo.textLength,
        );
        selection.editor.insertText(contextInfo.textOffset, result);
      }
    });
  }

  openCreateCompendiumRecordDialog() {
    const selection = this.requireLastSelection();
    if (!selection) {
      return;
    }

    const prompts = this.prompts.filter(
      (p) => p.type === PromptType.CreateCompendiumRecord,
    );

    if (prompts.length === 0) {
      this.toastr.error('No compendium record prompts available');
      return;
    }

    this.dialogRef = this.dialogService.open(GenerateTextComponent, {
      header: 'Create Compendium Record',
      width: '50vw',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      modal: true,
      closable: true,
      closeOnEscape: true,
      dismissableMask: true,
      data: <GenerateTextComponentData>{
        prompts: prompts,
        contextInfo: <CreateCompendiumRecordContextInfoDto>{
          $type: NovelTextGenerationType.CreateCompendiumRecord,
          novelId: this.novelId,
          chapterIndex: selection.chapterIndex,
          sectionIndex: selection.sectionIndex,
          textOffset: selection.range.index,
          textLength: selection.range.length,
          instructions: null,
        },
        instructionsRequired: true,
      },
    });

    this.dialogRef?.onClose.subscribe((request: GenerateTextRequestDto) => {
      if (request) {
        this.dialogRef = this.dialogService.open(GenerateTextResultComponent, {
          header: 'Create Compendium Record',
          width: '50vw',
          contentStyle: { overflow: 'auto' },
          baseZIndex: 10000,
          modal: true,
          closable: true,
          closeOnEscape: true,
          dismissableMask: true,
          data: <GenerateTextResultComponentData>{
            request: request,
            textToReplace: '',
          },
        });
        this.dialogRef?.onClose.subscribe(
          (result: string | 'back' | undefined) => {
            if (result === 'back') {
              this.openCreateCompendiumRecordDialog();
            } else if (result) {
              this.dialogRef = this.dialogService.open(
                GenerateCompendiumRecordResultComponent,
                {
                  header: 'Create Compendium Record',
                  width: '50vw',
                  contentStyle: { overflow: 'auto' },
                  baseZIndex: 10000,
                  modal: true,
                  closable: true,
                  closeOnEscape: true,
                  dismissableMask: true,
                  data: <GenerateCompendiumRecordComponentData>{
                    generatedText: result,
                    novelId: this.novelId,
                  },
                },
              );
              this.dialogRef?.onClose.subscribe((changed) => {
                if (changed === true) {
                  this.recordsChange.emit();
                }
              });
            }
          },
        );
      }
    });
  }

  addProseImage(chapterIndex: number, sectionIndex: number) {
    this.dialogRef = this.dialogService.open(ImageSourceSelectorComponent, {
      header: 'Add Image',
      width: '300px',
      modal: true,
      closable: true,
      dismissableMask: true,
    });

    this.dialogRef?.onClose.subscribe(
      (result: 'upload' | 'generate' | 'clipboard') => {
      if (result === 'upload') {
        this.uploadProseImageFile(chapterIndex, sectionIndex);
      } else if (result === 'generate') {
        this.generateProseImage(chapterIndex, sectionIndex);
      } else if (result === 'clipboard') {
        this.uploadClipboardProseImage(chapterIndex, sectionIndex);
      }
      },
    );
  }

  uploadProseImageFile(chapterIndex: number, sectionIndex: number) {
    const fileInput = document.createElement('input');
    fileInput.type = 'file';
    fileInput.accept = 'image/*,video/*';
    fileInput.onchange = () => {
      if (fileInput.files && fileInput.files.length > 0) {
        const file = fileInput.files[0];
        this.novelService
          .uploadProseImage(this.novelId, file)
          .subscribe((location: string) => {
            this.prose.chapters[chapterIndex].sections[sectionIndex].images =
              this.prose.chapters[chapterIndex].sections[
                sectionIndex
              ].images.concat(location);
            this.saveProse();
            fileInput.remove();
          });
      }
    };
    fileInput.click();
  }

  async uploadClipboardProseImage(
    chapterIndex: number,
    sectionIndex: number,
  ): Promise<void> {
    try {
      const file = await readImageFileFromClipboard();
      this.novelService
        .uploadProseImage(this.novelId, file)
        .subscribe((location: string) => {
          this.prose.chapters[chapterIndex].sections[sectionIndex].images =
            this.prose.chapters[chapterIndex].sections[
              sectionIndex
            ].images.concat(location);
          this.saveProse();
        });
    } catch (error) {
      this.toastr.error(
        error instanceof Error
          ? error.message
          : 'Failed to read image from clipboard.',
      );
    }
  }

  generateProseImage(chapterIndex: number, sectionIndex: number) {
    this.dialogRef = this.dialogService.open(GenerateImageComponent, {
      header: 'Generate Image',
      width: '50vw',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      closable: true,
      closeOnEscape: true,
      modal: true,
      dismissableMask: true,
    });

    this.dialogRef?.onClose.subscribe((image: Blob) => {
      if (image) {
        const file = new File([image], 'generated-image.png', {
          type: 'image/png',
        });
        this.novelService
          .uploadProseImage(this.novelId, file)
          .subscribe((location: string) => {
            this.prose.chapters[chapterIndex].sections[sectionIndex].images =
              this.prose.chapters[chapterIndex].sections[
                sectionIndex
              ].images.concat(location);
            this.saveProse();
          });
      }
    });
  }

  removeProseImage(
    chapterIndex: number,
    sectionIndex: number,
    imageId: string,
  ) {
    this.confirmationService.confirm({
      message: 'Are you sure you want to remove this image?',
      header: 'Confirm Image Removal',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.novelService.deleteProseImage(this.novelId, imageId).subscribe({
          next: () => {
            this.prose.chapters[chapterIndex].sections[sectionIndex].images =
              this.prose.chapters[chapterIndex].sections[
                sectionIndex
              ].images.filter((img) => img !== imageId);
            this.saveProse();
          },
          error: () => {
            this.toastr.error('Could not remove image from the server.');
          },
        });
      },
    });
  }

  openRecordOverridesDialog(chapterIndex: number, sectionIndex: number) {
    const section = this.prose.chapters[chapterIndex].sections[sectionIndex];
    const availableRecords = this.compendia
      ? this.compendia.flatMap((c) => c.records)
      : [];

    this.dialogRef = this.dialogService.open(RecordOverridesEditorComponent, {
      header: 'Record Overrides',
      width: '50vw',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      modal: true,
      closable: true,
      closeOnEscape: true,
      dismissableMask: true,
      focusOnShow: false,
      data: <RecordOverridesEditorComponentData>{
        recordOverrides: section.recordOverrides || [],
        availableRecords: availableRecords,
      },
    });

    this.dialogRef?.onClose.subscribe((overrides: RecordOverride[]) => {
      if (overrides) {
        section.recordOverrides = overrides;
        this.saveProse();
      }
    });
  }
}
