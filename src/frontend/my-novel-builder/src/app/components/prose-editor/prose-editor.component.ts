import {
  Component,
  EventEmitter,
  Input,
  Output,
  inject,
} from '@angular/core';
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
  CreateCompendiumRecordContextInfoDto,
  GenerateTextContextInfoDto,
  GenerateTextRequestDto,
  ReplaceTextContextInfoDto,
  SummarizeTextContextInfoDto,
  NovelTextGenerationType,
} from '../../types/dtos/generate/generate-text-request.dto';
import Quill from 'quill';
import { DialogService } from 'primeng/dynamicdialog';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import { ProseTtsService } from './prose-tts.service';
import { ProseMediaService } from './prose-media.service';
import { ProseGenerationDialogService } from './prose-generation-dialog.service';
import {
  ProseRpgCommand,
  ProseRpgPanelComponent,
} from './prose-rpg-panel.component';
import {
  appendMarkdownToHtml,
  calculateReadingTimeMinutes,
  countChapterWords,
  htmlToPlainText,
  insertMarkdownIntoEditor,
} from './prose-text.utils';

interface LastSelection {
  editor: Quill;
  range: Range;
  text: string;
  chapterIndex: number;
  sectionIndex: number;
}

interface GenerateTextDialogPrefill {
  initialPromptId?: string;
  initialModel?: string;
  initialInstructions?: string;
}

interface RpgAppendTarget {
  editor: Quill | null;
  chapterIndex: number;
  sectionIndex: number;
  section: Section;
  textOffset: number;
  editorOffset: number;
}

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
    ProseRpgPanelComponent,
  ],
  providers: [
    DialogService,
    ConfirmationService,
    ProseGenerationDialogService,
    ProseMediaService,
    ProseTtsService,
  ],
})
export class ProseEditorComponent {
  @Input() novelId!: string;
  @Input() prose!: Prose;
  @Input() selectedChapterIndex: number | null = null;
  @Input() rpgMode = false;
  @Input() prompts!: PromptDto[];
  @Input() compendia: CompendiumDto[] | null = null;
  @Output() proseChange: EventEmitter<Prose> = new EventEmitter<Prose>();
  @Output() recordsChange: EventEmitter<void> = new EventEmitter<void>();
  @Output() proseImageClicked: EventEmitter<string> =
    new EventEmitter<string>();
  private confirmationService = inject(ConfirmationService);
  readonly toastr: ToastrService = inject(ToastrService);
  readonly generateTextService: GenerateTextService =
    inject(GenerateTextService);
  private readonly proseGenerationDialogService = inject(
    ProseGenerationDialogService,
  );
  private readonly proseMediaService = inject(ProseMediaService);
  private readonly proseTtsService = inject(ProseTtsService);
  showEditorControls = false;
  editorControlsPosition: { x: number; y: number } = { x: 0, y: 0 };
  lastSelection: LastSelection | null = null;
  private readonly sectionEditors = new Map<string, Quill>();
  isRpgGenerating = false;

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

  sendRpgPrompt(
    command: ProseRpgCommand,
    panel: ProseRpgPanelComponent,
  ): void {
    const target = this.getRpgAppendTarget();
    if (!target) {
      return;
    }

    panel.clearInput();
    this.isRpgGenerating = true;
    this.saveProse();

    const request: GenerateTextRequestDto = {
      model: command.model,
      promptId: command.promptId,
      contextInfo: <GenerateTextContextInfoDto>{
        $type: NovelTextGenerationType.GenerateText,
        novelId: this.novelId,
        chapterIndex: target.chapterIndex,
        sectionIndex: target.sectionIndex,
        textOffset: target.textOffset,
        instructions: `${command.action === 'do' ? 'Do' : 'Say'}: ${command.input}`,
      },
    };

    this.generateTextService.generateText(request).subscribe({
      next: async (update) => {
        if (!update.isComplete) {
          return;
        }

        const generatedText = update.content;

        if (!generatedText.trim()) {
          this.toastr.error('No RPG response was generated.');
          this.isRpgGenerating = false;
          return;
        }

        if (target.editor) {
          await insertMarkdownIntoEditor(
            target.editor,
            target.editorOffset,
            generatedText,
          );
          target.section.text = target.editor.getSemanticHTML();
        } else {
          target.section.text = await appendMarkdownToHtml(
            target.section.text,
            generatedText,
          );
        }

        this.isRpgGenerating = false;
        this.saveProse();
      },
      error: (error) => {
        console.error('Error generating RPG text:', error);
        panel.restoreInput(command.input);
        this.isRpgGenerating = false;
        this.toastr.error('Failed to generate RPG response.');
      },
    });
  }

  private getRpgAppendTarget(): RpgAppendTarget | null {
    const targetIndexes = this.getLastSectionIndexes();
    if (!targetIndexes) {
      this.toastr.error('Add a section before using RPG mode.');
      return null;
    }

    const { chapterIndex, sectionIndex } = targetIndexes;
    const section = this.prose.chapters[chapterIndex].sections[sectionIndex];
    const editor = this.sectionEditors.get(
      this.getSectionEditorKey(chapterIndex, sectionIndex),
    ) ?? null;

    if (editor) {
      section.text = editor.getSemanticHTML();
    }

    const editorOffset = editor ? Math.max(0, editor.getLength() - 1) : 0;
    const textOffset = editor
      ? editor.getText(0, editorOffset).length
      : htmlToPlainText(section.text).length;

    return {
      editor,
      chapterIndex,
      sectionIndex,
      section,
      textOffset,
      editorOffset,
    };
  }

  private getLastSectionIndexes(): { chapterIndex: number; sectionIndex: number } | null {
    if (this.prose.chapters.length === 0) {
      return null;
    }

    const chapterIndex = this.prose.chapters.length - 1;
    const chapter = this.prose.chapters[chapterIndex];
    if (chapter.sections.length === 0) {
      return null;
    }

    return {
      chapterIndex,
      sectionIndex: chapter.sections.length - 1,
    };
  }

  getChapterWordCount(chapter: Prose['chapters'][number]): number {
    return countChapterWords(chapter);
  }

  getReadingTimeMinutes(wordCount: number): number {
    return calculateReadingTimeMinutes(wordCount);
  }

  private requireLastSelection(): LastSelection | null {
    if (this.lastSelection) {
      return this.lastSelection;
    }

    this.toastr.error('Please select text before using this action.');
    return null;
  }

  preventReturnKey(event: KeyboardEvent) {
    if (event.key === 'Enter') {
      event.preventDefault();
    }
  }

  editorInit(quill: Quill, chapterIndex: number, sectionIndex: number) {
    this.sectionEditors.set(this.getSectionEditorKey(chapterIndex, sectionIndex), quill);

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

  private getSectionEditorKey(chapterIndex: number, sectionIndex: number): string {
    return `${chapterIndex}:${sectionIndex}`;
  }

  async textToSpeech(chapterIndex: number, sectionIndex: number) {
    const section = this.prose.chapters[chapterIndex].sections[sectionIndex];
    await this.proseTtsService.playSection({
      novelId: this.novelId,
      prompts: this.prompts ?? [],
      chapterIndex,
      sectionIndex,
      narratorText: htmlToPlainText(section.text),
    });
  }

  openGenerateSectionSummaryDialog(chapterIndex: number, sectionIndex: number) {
    const prompts = this.prompts.filter(
      (p) => p.type === PromptType.SummarizeText,
    );

    if (prompts.length === 0) {
      this.toastr.error('No summarization prompts available');
      return;
    }

    this.proseGenerationDialogService
      .openTextRequestDialog('Generate Section Summary', {
        prompts: prompts,
        instructionsRequired: false,
        contextInfo: <SummarizeTextContextInfoDto>{
          $type: NovelTextGenerationType.SummarizeText,
          novelId: this.novelId,
          chapterIndex: chapterIndex,
          sectionIndex: sectionIndex,
        },
      })
      .subscribe((request) => {
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
      next: (update) => {
        if (update.content.length > 0) {
          this.prose.chapters[chapterIndex].sections[sectionIndex].summary =
            update.content;
        }

        if (update.isComplete) {
          this.saveProse();
        }
      },
    });
  }

  openGenerateTextDialog(prefill: GenerateTextDialogPrefill = {}) {
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

    this.proseGenerationDialogService
      .openTextRequestDialog('Generate Text', {
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
        initialPromptId: prefill.initialPromptId,
        initialModel: prefill.initialModel,
        initialInstructions: prefill.initialInstructions,
      })
      .subscribe((request) => {
        if (request) {
          this.openGenerateTextResultDialog(request);
        }
      });
  }

  openGenerateTextResultDialog(request: GenerateTextRequestDto) {
    this.proseGenerationDialogService
      .openTextResultDialog('Generate Text', {
        request: request,
        textToReplace: this.lastSelection?.text ?? '',
      })
      .subscribe(async (result) => {
        const contextInfo = request.contextInfo as GenerateTextContextInfoDto;

        if (result === 'back') {
          this.openGenerateTextDialog({
            initialPromptId: request.promptId,
            initialModel: request.model,
            initialInstructions: contextInfo.instructions ?? undefined,
          });
        } else if (result) {
          const selection = this.lastSelection;
          if (!selection) {
            this.toastr.error('Selection is no longer available.');
            return;
          }

          // Append the generated text at the end of the range in the Quill editor.
          await insertMarkdownIntoEditor(
            selection.editor,
            contextInfo.textOffset,
            result,
          );

          const section =
            this.prose.chapters[contextInfo.chapterIndex].sections[
              contextInfo.sectionIndex
            ];

          section.text = selection.editor.getSemanticHTML();
          this.saveProse();
        }
      });
  }

  openGenerateStorySuggestionsDialog() {
    const selection = this.requireLastSelection();
    if (!selection) {
      return;
    }

    if (selection.range.length > 0) {
      this.toastr.error(
        'Story suggestions are only available with no text selected.',
      );
      return;
    }

    const prompts = this.prompts.filter(
      (p) => p.type === PromptType.SuggestStoryDevelopments,
    );

    if (prompts.length === 0) {
      this.toastr.error('No story suggestion prompts available');
      return;
    }

    this.saveProse();

    this.proseGenerationDialogService
      .openStorySuggestionsDialog({
        prompts,
        novelId: this.novelId,
        chapterIndex: selection.chapterIndex,
        sectionIndex: selection.sectionIndex,
        textOffset: selection.range.index,
      })
      .subscribe((result) => {
        if (!result) {
          return;
        }

        this.openGenerateTextDialog({
          initialModel: result.model,
          initialInstructions: result.instructions,
        });
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

    this.proseGenerationDialogService
      .openTextRequestDialog('Replace Text', {
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
      })
      .subscribe((request) => {
        if (request) {
          this.openReplaceTextResultDialog(request);
        }
      });
  }

  openReplaceTextResultDialog(request: GenerateTextRequestDto) {
    this.proseGenerationDialogService
      .openTextResultDialog('Replace Text', {
        request: request,
        textToReplace: this.lastSelection?.text ?? '',
      })
      .subscribe(async (result) => {
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
          await insertMarkdownIntoEditor(
            selection.editor,
            contextInfo.textOffset,
            result,
          );

          const section =
            this.prose.chapters[contextInfo.chapterIndex].sections[
              contextInfo.sectionIndex
            ];

          section.text = selection.editor.getSemanticHTML();
          this.saveProse();
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

    this.proseGenerationDialogService
      .openTextRequestDialog('Create Compendium Record', {
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
      })
      .subscribe((request) => {
        if (request) {
          this.proseGenerationDialogService
            .openTextResultDialog('Create Compendium Record', {
              request: request,
              textToReplace: '',
            })
            .subscribe((result) => {
              if (result === 'back') {
                this.openCreateCompendiumRecordDialog();
              } else if (result) {
                this.proseGenerationDialogService
                  .openCompendiumRecordResultDialog({
                    generatedText: result,
                    novelId: this.novelId,
                  })
                  .subscribe((changed) => {
                    if (changed === true) {
                      this.recordsChange.emit();
                    }
                  });
              }
            });
        }
      });
  }

  addProseImage(chapterIndex: number, sectionIndex: number) {
    this.proseMediaService.selectSource().subscribe((source) => {
      if (source === 'upload') {
        this.uploadProseImageFile(chapterIndex, sectionIndex);
      } else if (source === 'generate') {
        this.generateProseImage(chapterIndex, sectionIndex);
      } else if (source === 'clipboard') {
        this.uploadClipboardProseImage(chapterIndex, sectionIndex);
      }
    });
  }

  uploadProseImageFile(chapterIndex: number, sectionIndex: number) {
    this.proseMediaService
      .selectFileAndUpload(this.novelId)
      .subscribe((location) => {
        this.appendProseImage(chapterIndex, sectionIndex, location);
      });
  }

  async uploadClipboardProseImage(
    chapterIndex: number,
    sectionIndex: number,
  ): Promise<void> {
    try {
      const location = await this.proseMediaService.uploadClipboardImage(
        this.novelId,
      );
      this.appendProseImage(chapterIndex, sectionIndex, location);
    } catch (error) {
      this.toastr.error(
        error instanceof Error
          ? error.message
          : 'Failed to read image from clipboard.',
      );
    }
  }

  generateProseImage(chapterIndex: number, sectionIndex: number) {
    this.proseMediaService
      .generateAndUpload(this.novelId)
      .subscribe((location) => {
        this.appendProseImage(chapterIndex, sectionIndex, location);
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
        this.proseMediaService.deleteImage(this.novelId, imageId).subscribe({
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

  private appendProseImage(
    chapterIndex: number,
    sectionIndex: number,
    location: string,
  ): void {
    const section = this.prose.chapters[chapterIndex].sections[sectionIndex];
    section.images = section.images.concat(location);
    this.saveProse();
  }

  openRecordOverridesDialog(chapterIndex: number, sectionIndex: number) {
    const section = this.prose.chapters[chapterIndex].sections[sectionIndex];
    const availableRecords = this.compendia
      ? this.compendia.flatMap((c) => c.records)
      : [];

    this.proseGenerationDialogService
      .openRecordOverridesDialog({
        recordOverrides: section.recordOverrides || [],
        availableRecords: availableRecords,
        prose: this.prose,
        chapterIndex,
        sectionIndex,
      })
      .subscribe((overrides) => {
        if (overrides) {
          section.recordOverrides = overrides;
          this.saveProse();
        }
      });
  }
}
