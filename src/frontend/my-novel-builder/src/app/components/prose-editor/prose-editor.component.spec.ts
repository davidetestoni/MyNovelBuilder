import { TestBed } from '@angular/core/testing';
import { ConfirmationService } from 'primeng/api';
import type { Confirmation } from 'primeng/api';
import { Subject, of, throwError } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import type {
  Blur,
  EditorChangeSelection,
  Range,
} from 'ngx-quill';
import type Quill from 'quill';
import { GenerateTextService } from '../../services/generate-text.service';
import type { GenerateTextStreamUpdate } from '../../services/generate-text.service';
import type {
  GenerateTextContextInfoDto,
  GenerateTextRequestDto,
  ReplaceTextContextInfoDto,
} from '../../types/dtos/generate/generate-text-request.dto';
import { NovelTextGenerationType } from '../../types/dtos/generate/generate-text-request.dto';
import type { Prose, Section } from '../../types/dtos/novel/prose';
import type { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { PromptType } from '../../types/enums/prompt-type';
import { ProseEditorComponent } from './prose-editor.component';
import { ProseGenerationDialogService } from './prose-generation-dialog.service';
import { ProseMediaService } from './prose-media.service';
import {
  ProseRpgCommand,
  ProseRpgPanelComponent,
} from './prose-rpg-panel.component';
import { ProseTtsService } from './prose-tts.service';

describe('ProseEditorComponent', () => {
  let component: ProseEditorComponent;
  let confirmationService: jasmine.SpyObj<ConfirmationService>;
  let toastr: jasmine.SpyObj<ToastrService>;
  let generateTextService: jasmine.SpyObj<GenerateTextService>;
  let proseGenerationDialogService: jasmine.SpyObj<
    ProseGenerationDialogService
  >;
  let proseMediaService: jasmine.SpyObj<ProseMediaService>;
  let proseTtsService: jasmine.SpyObj<ProseTtsService>;

  const createSection = (
    summary = 'Summary',
    text = '<p>Text</p>',
  ): Section => ({
    summary,
    text,
    images: [],
    recordOverrides: [],
  });

  const createProse = (): Prose => ({
    chapters: [
      {
        title: 'Chapter 1',
        sections: [createSection()],
        storyEvents: [],
      },
    ],
  });

  const createPrompt = (type: PromptType, id = `prompt-${type}`): PromptDto => ({
    id,
    createdAt: '',
    updatedAt: '',
    name: id,
    type,
    messages: [],
  });

  const createQuill = () => {
    const quill = {
      clipboard: {
        addMatcher: jasmine.createSpy('addMatcher'),
        dangerouslyPasteHTML: jasmine.createSpy('dangerouslyPasteHTML'),
      },
      container: {
        getBoundingClientRect: jasmine
          .createSpy('getBoundingClientRect')
          .and.returnValue({ left: 30, top: 40 }),
      },
      deleteText: jasmine.createSpy('deleteText'),
      getBounds: jasmine
        .createSpy('getBounds')
        .and.returnValue({ right: 50, bottom: 60 }),
      getLength: jasmine.createSpy('getLength').and.returnValue(6),
      getSemanticHTML: jasmine
        .createSpy('getSemanticHTML')
        .and.returnValue('<p>Editor text</p>'),
      getText: jasmine.createSpy('getText').and.returnValue('Hello'),
    };

    return quill as unknown as Quill;
  };

  const setSelection = (
    quill = createQuill(),
    range: Range = { index: 2, length: 3 },
  ): Quill => {
    component.lastSelection = {
      editor: quill,
      range,
      text: range.length ? 'llo' : '',
      chapterIndex: 0,
      sectionIndex: 0,
    };
    return quill;
  };

  const createRequest = (
    type: NovelTextGenerationType = NovelTextGenerationType.GenerateText,
  ): GenerateTextRequestDto => {
    const contextInfo =
      type === NovelTextGenerationType.ReplaceText
        ? ({
            $type: NovelTextGenerationType.ReplaceText,
            novelId: 'novel-1',
            chapterIndex: 0,
            sectionIndex: 0,
            textOffset: 2,
            textLength: 3,
            instructions: 'replace it',
          } satisfies ReplaceTextContextInfoDto)
        : ({
            $type: NovelTextGenerationType.GenerateText,
            novelId: 'novel-1',
            chapterIndex: 0,
            sectionIndex: 0,
            textOffset: 2,
            instructions: 'continue it',
          } satisfies GenerateTextContextInfoDto);

    return {
      model: 'model-1',
      promptId: 'prompt-1',
      contextInfo,
    };
  };

  const flushAsyncSubscriber = async (): Promise<void> => {
    await Promise.resolve();
    await Promise.resolve();
  };

  beforeEach(() => {
    confirmationService = jasmine.createSpyObj<ConfirmationService>(
      'ConfirmationService',
      ['confirm'],
    );
    toastr = jasmine.createSpyObj<ToastrService>('ToastrService', [
      'clear',
      'error',
      'info',
      'warning',
    ]);
    toastr.info.and.returnValue({ toastId: 41 } as never);
    generateTextService = jasmine.createSpyObj<GenerateTextService>(
      'GenerateTextService',
      ['generateText'],
    );
    proseGenerationDialogService =
      jasmine.createSpyObj<ProseGenerationDialogService>(
        'ProseGenerationDialogService',
        [
          'openCompendiumRecordResultDialog',
          'openRecordOverridesDialog',
          'openStorySuggestionsDialog',
          'openTextRequestDialog',
          'openTextResultDialog',
        ],
      );
    proseGenerationDialogService.openCompendiumRecordResultDialog.and
      .returnValue(of());
    proseGenerationDialogService.openRecordOverridesDialog.and.returnValue(
      of(),
    );
    proseGenerationDialogService.openStorySuggestionsDialog.and.returnValue(
      of(),
    );
    proseGenerationDialogService.openTextRequestDialog.and.returnValue(of());
    proseGenerationDialogService.openTextResultDialog.and.returnValue(of());
    proseMediaService = jasmine.createSpyObj<ProseMediaService>(
      'ProseMediaService',
      [
        'deleteImage',
        'generateAndUpload',
        'selectFileAndUpload',
        'selectSource',
        'uploadClipboardImage',
      ],
    );
    proseMediaService.selectSource.and.returnValue(of());
    proseTtsService = jasmine.createSpyObj<ProseTtsService>(
      'ProseTtsService',
      ['playSection'],
    );
    proseTtsService.playSection.and.resolveTo();

    TestBed.configureTestingModule({
      providers: [
        { provide: ConfirmationService, useValue: confirmationService },
        { provide: ToastrService, useValue: toastr },
        { provide: GenerateTextService, useValue: generateTextService },
        {
          provide: ProseGenerationDialogService,
          useValue: proseGenerationDialogService,
        },
        { provide: ProseMediaService, useValue: proseMediaService },
        { provide: ProseTtsService, useValue: proseTtsService },
      ],
    });

    spyOn(console, 'error');
    component = TestBed.runInInjectionContext(() => new ProseEditorComponent());
    component.novelId = 'novel-1';
    component.prose = createProse();
    component.prompts = [];
  });

  describe('prose mutations and derived values', () => {
    it('adds a chapter with initialized collections and emits the prose', () => {
      const originalChapters = component.prose.chapters;
      const emit = spyOn(component.proseChange, 'emit');

      component.addChapter();

      expect(component.prose.chapters).not.toBe(originalChapters);
      expect(component.prose.chapters[1]).toEqual({
        title: 'Chapter 2',
        sections: [],
        storyEvents: [],
      });
      expect(emit).toHaveBeenCalledOnceWith(component.prose);
    });

    it('removes an empty chapter and emits the prose', () => {
      component.prose.chapters.push({
        title: 'Empty chapter',
        sections: [],
        storyEvents: [],
      });
      const emit = spyOn(component.proseChange, 'emit');

      component.removeChapter(1);

      expect(component.prose.chapters.map((chapter) => chapter.title)).toEqual([
        'Chapter 1',
      ]);
      expect(emit).toHaveBeenCalledOnceWith(component.prose);
    });

    it('does not remove a non-empty chapter', () => {
      const emit = spyOn(component.proseChange, 'emit');

      component.removeChapter(0);

      expect(component.prose.chapters).toHaveSize(1);
      expect(toastr.error).toHaveBeenCalledOnceWith(
        'Cannot remove a chapter that is not empty. Please remove all sections first.',
      );
      expect(emit).not.toHaveBeenCalled();
    });

    it('adds an initialized section and emits the prose', () => {
      const originalSections = component.prose.chapters[0].sections;
      const emit = spyOn(component.proseChange, 'emit');

      component.addSection(0);

      expect(component.prose.chapters[0].sections).not.toBe(originalSections);
      expect(component.prose.chapters[0].sections[1]).toEqual({
        summary: '[Missing summary]',
        text: '',
        images: [],
        recordOverrides: [],
      });
      expect(emit).toHaveBeenCalledOnceWith(component.prose);
    });

    it('removes a section only after confirmation is accepted', () => {
      component.prose.chapters[0].sections.push(createSection('Second'));
      const emit = spyOn(component.proseChange, 'emit');

      component.removeSection(0, 0);

      expect(component.prose.chapters[0].sections).toHaveSize(2);
      const confirmation = confirmationService.confirm.calls.mostRecent()
        .args[0] as Confirmation;
      confirmation.accept?.();

      expect(component.prose.chapters[0].sections).toEqual([
        jasmine.objectContaining({ summary: 'Second' }),
      ]);
      expect(emit).toHaveBeenCalledOnceWith(component.prose);
    });

    it('updates non-empty chapter titles and restores empty ones', () => {
      const emit = spyOn(component.proseChange, 'emit');
      const title = document.createElement('div');
      title.innerText = 'Renamed chapter';

      component.updateChapterTitle(0, { target: title } as unknown as Event);
      expect(component.prose.chapters[0].title).toBe('Renamed chapter');

      title.innerText = '   ';
      component.updateChapterTitle(0, { target: title } as unknown as Event);

      expect(title.innerText).toBe('Renamed chapter');
      expect(emit).toHaveBeenCalledTimes(1);
    });

    it('updates section text and supplies the fallback for an empty summary', () => {
      const section = component.prose.chapters[0].sections[0];
      const emit = spyOn(component.proseChange, 'emit');
      const blurEvent = {
        editor: { getSemanticHTML: () => '<p>Updated text</p>' },
      } as unknown as Blur;
      const summary = document.createElement('div');
      summary.innerText = '   ';

      component.updateSectionText(section, blurEvent);
      component.updateSectionSummary(
        section,
        { target: summary } as unknown as Event,
      );

      expect(section.text).toBe('<p>Updated text</p>');
      expect(summary.innerText).toBe('[Missing summary]');
      expect(section.summary).toBe('[Missing summary]');
      expect(emit).toHaveBeenCalledTimes(2);
    });

    it('builds image URLs and calculates normalized word counts and reading times', () => {
      component.prose.chapters[0].sections = [
        createSection('One', '<p>One   two</p>'),
        createSection('Two', '<p>three</p><p>four</p>'),
      ];

      expect(component.getImageUrl('cover.png')).toContain(
        '/novels/novel-1/prose-images/cover.png',
      );
      expect(component.getChapterWordCount(component.prose.chapters[0])).toBe(4);
      expect(component.getChapterWordCount({
        title: '',
        sections: [],
        storyEvents: [],
      })).toBe(0);
      expect(component.getReadingTimeMinutes(0)).toBe(0);
      expect(component.getReadingTimeMinutes(239)).toBe(2);
    });
  });

  describe('editor selection', () => {
    it('registers the editor and strips pasted foreground and background colors', () => {
      const quill = createQuill();

      component.editorInit(quill, 1, 2);

      expect(quill.clipboard.addMatcher).toHaveBeenCalled();
      const matcher = (
        quill.clipboard.addMatcher as jasmine.Spy
      ).calls.mostRecent().args[1];
      const delta = [
        { attributes: { color: 'red', background: 'blue', bold: true } },
        {},
      ];
      expect(matcher(document.createElement('span'), delta)).toBe(delta);
      expect(delta[0].attributes).toEqual({
        color: '',
        background: '',
        bold: true,
      });
    });

    it('tracks a selection and positions controls relative to the prose editor', () => {
      const host = document.createElement('div');
      host.id = 'prose-editor';
      document.body.appendChild(host);
      spyOn(host, 'getBoundingClientRect').and.returnValue({
        left: 10,
        top: 15,
      } as DOMRect);
      const quill = createQuill();
      const event = {
        event: 'selection-change',
        editor: quill,
        range: { index: 4, length: 2 },
      } as unknown as EditorChangeSelection;

      component.editorChange(event, 1, 2);

      expect(component.showEditorControls).toBeTrue();
      expect(component.editorControlsPosition).toEqual({ x: 80, y: 75 });
      expect(component.lastSelection).toEqual(
        jasmine.objectContaining({
          editor: quill,
          chapterIndex: 1,
          sectionIndex: 2,
          text: 'Hello',
        }),
      );
      expect((quill.getBounds as jasmine.Spy)).toHaveBeenCalledOnceWith({
        index: 5,
        length: 1,
      });
      host.remove();
    });

    it('hides controls for missing hosts, cleared selections, and missing bounds', () => {
      const quill = createQuill();
      component.showEditorControls = true;
      const event = {
        event: 'selection-change',
        editor: quill,
        range: { index: 0, length: 0 },
      } as unknown as EditorChangeSelection;

      component.editorChange(event, 0, 0);
      expect(component.showEditorControls).toBeFalse();

      const host = document.createElement('div');
      host.id = 'prose-editor';
      document.body.appendChild(host);
      component.showEditorControls = true;
      component.editorChange(
        { ...event, range: null } as unknown as EditorChangeSelection,
        0,
        0,
      );
      expect(component.showEditorControls).toBeFalse();

      component.showEditorControls = true;
      (quill.getBounds as jasmine.Spy).and.returnValue(null);
      component.editorChange(event, 0, 0);
      expect(component.showEditorControls).toBeFalse();
      host.remove();
    });

    it('ignores content changes and prevents only the return key', () => {
      component.showEditorControls = true;
      component.editorChange(
        { event: 'text-change' } as never,
        0,
        0,
      );
      const enter = new KeyboardEvent('keydown', { key: 'Enter' });
      const letter = new KeyboardEvent('keydown', { key: 'a' });
      spyOn(enter, 'preventDefault');
      spyOn(letter, 'preventDefault');

      component.preventReturnKey(enter);
      component.preventReturnKey(letter);

      expect(component.showEditorControls).toBeTrue();
      expect(enter.preventDefault).toHaveBeenCalled();
      expect(letter.preventDefault).not.toHaveBeenCalled();
    });
  });

  describe('RPG mode', () => {
    const createCommand = (
      overrides: Partial<ProseRpgCommand> = {},
    ): ProseRpgCommand => ({
      action: 'do',
      input: 'Open the door',
      promptId: 'rpg-prompt',
      model: 'rpg-model',
      ...overrides,
    });

    const createPanel = (): jasmine.SpyObj<ProseRpgPanelComponent> =>
      jasmine.createSpyObj<ProseRpgPanelComponent>('ProseRpgPanelComponent', [
        'clearInput',
        'restoreInput',
      ]);

    it('reports that a section is required before RPG generation', () => {
      component.prose.chapters[0].sections = [];
      const panel = createPanel();

      component.sendRpgPrompt(createCommand(), panel);

      expect(toastr.error).toHaveBeenCalledOnceWith(
        'Add a section before using RPG mode.',
      );
      expect(panel.clearInput).not.toHaveBeenCalled();
      expect(generateTextService.generateText).not.toHaveBeenCalled();
    });

    it('appends completed RPG markdown without a mounted editor', async () => {
      const panel = createPanel();
      const command = createCommand({ action: 'say', input: 'Hello there' });
      generateTextService.generateText.and.returnValue(
        of({ content: '**Reply**', isComplete: true }),
      );
      const emit = spyOn(component.proseChange, 'emit');

      component.sendRpgPrompt(command, panel);
      await flushAsyncSubscriber();

      expect(generateTextService.generateText.calls.mostRecent().args[0]).toEqual({
        model: 'rpg-model',
        promptId: 'rpg-prompt',
        contextInfo: jasmine.objectContaining({
          $type: NovelTextGenerationType.GenerateText,
          novelId: 'novel-1',
          chapterIndex: 0,
          sectionIndex: 0,
          textOffset: 4,
          instructions: 'Say: Hello there',
        }),
      });
      expect(component.prose.chapters[0].sections[0].text).toContain('Reply');
      expect(panel.clearInput).toHaveBeenCalledTimes(1);
      expect(component.isRpgGenerating).toBeFalse();
      expect(emit).toHaveBeenCalledTimes(2);
    });

    it('appends RPG output to a mounted editor at its final offset', async () => {
      const quill = createQuill();
      component.editorInit(quill, 0, 0);
      const panel = createPanel();
      generateTextService.generateText.and.returnValue(
        of({ content: 'Next', isComplete: true }),
      );

      component.sendRpgPrompt(createCommand({ input: 'continue' }), panel);
      await flushAsyncSubscriber();

      expect(
        quill.clipboard.dangerouslyPasteHTML as jasmine.Spy,
      ).toHaveBeenCalledOnceWith(5, jasmine.stringContaining('Next'), 'user');
      expect(component.prose.chapters[0].sections[0].text).toBe(
        '<p>Editor text</p>',
      );
      expect(generateTextService.generateText.calls.mostRecent().args[0])
        .withContext('uses editor plain-text length for the backend offset')
        .toEqual(jasmine.objectContaining({
          contextInfo: jasmine.objectContaining({ textOffset: 5 }),
        }));
    });

    it('handles empty and failed RPG responses and restores input after errors', () => {
      const panel = createPanel();
      generateTextService.generateText.and.returnValue(
        of({ content: '   ', isComplete: true }),
      );

      component.sendRpgPrompt(createCommand({ input: 'first' }), panel);
      expect(toastr.error).toHaveBeenCalledWith('No RPG response was generated.');
      expect(component.isRpgGenerating).toBeFalse();
      expect(panel.restoreInput).not.toHaveBeenCalled();

      toastr.error.calls.reset();
      generateTextService.generateText.and.returnValue(
        throwError(() => new Error('network')),
      );
      component.sendRpgPrompt(createCommand({ input: 'retry this' }), panel);

      expect(panel.restoreInput).toHaveBeenCalledOnceWith('retry this');
      expect(component.isRpgGenerating).toBeFalse();
      expect(toastr.error).toHaveBeenCalledOnceWith(
        'Failed to generate RPG response.',
      );
    });
  });

  describe('text to speech', () => {
    it('delegates playback with normalized section text and context', async () => {
      component.prose.chapters[0].sections[0].text =
        '<p>Hello</p><p>world</p>';
      component.prompts = [createPrompt(PromptType.PrepareImmersiveTts)];

      await component.textToSpeech(0, 0);

      expect(proseTtsService.playSection).toHaveBeenCalledOnceWith({
        novelId: 'novel-1',
        prompts: component.prompts,
        chapterIndex: 0,
        sectionIndex: 0,
        narratorText: 'Hello world',
      });
    });
  });

  describe('generation dialogs', () => {
    it('requires a matching summary prompt', () => {
      component.openGenerateSectionSummaryDialog(0, 0);

      expect(toastr.error).toHaveBeenCalledOnceWith(
        'No summarization prompts available',
      );
      expect(
        proseGenerationDialogService.openTextRequestDialog,
      ).not.toHaveBeenCalled();
    });

    it('requires a selection and a matching generation prompt', () => {
      component.openGenerateTextDialog();
      expect(toastr.error).toHaveBeenCalledOnceWith(
        'Please select text before using this action.',
      );

      toastr.error.calls.reset();
      setSelection();
      component.openGenerateTextDialog();
      expect(toastr.error).toHaveBeenCalledOnceWith(
        'No generation prompts available',
      );
      expect(
        proseGenerationDialogService.openTextRequestDialog,
      ).not.toHaveBeenCalled();
    });

    it('opens the generate-text dialog with selection context and prefill', () => {
      setSelection();
      component.prompts = [
        createPrompt(PromptType.GenerateText, 'generate'),
        createPrompt(PromptType.ReplaceText, 'replace'),
      ];
      const close$ = new Subject<GenerateTextRequestDto>();
      proseGenerationDialogService.openTextRequestDialog.and.returnValue(
        close$,
      );
      const openResult = spyOn(component, 'openGenerateTextResultDialog');
      const emit = spyOn(component.proseChange, 'emit');
      const request = createRequest();

      component.openGenerateTextDialog({
        initialPromptId: 'recent-prompt',
        initialModel: 'recent-model',
        initialInstructions: 'keep going',
      });
      close$.next(request);

      const [header, data] =
        proseGenerationDialogService.openTextRequestDialog.calls.mostRecent()
          .args;
      expect(header).toBe('Generate Text');
      expect(data).toEqual(
        jasmine.objectContaining({
          prompts: [jasmine.objectContaining({ id: 'generate' })],
          initialPromptId: 'recent-prompt',
          initialModel: 'recent-model',
          initialInstructions: 'keep going',
          contextInfo: jasmine.objectContaining({
            chapterIndex: 0,
            sectionIndex: 0,
            textOffset: 2,
          }),
        }),
      );
      expect(emit).toHaveBeenCalledOnceWith(component.prose);
      expect(openResult).toHaveBeenCalledOnceWith(request);
    });

    it('navigates back from generate results with the previous choices', () => {
      const close$ = new Subject<string | 'back' | undefined>();
      proseGenerationDialogService.openTextResultDialog.and.returnValue(
        close$,
      );
      const reopen = spyOn(component, 'openGenerateTextDialog');
      const request = createRequest();

      component.openGenerateTextResultDialog(request);
      close$.next('back');

      expect(
        proseGenerationDialogService.openTextResultDialog,
      ).toHaveBeenCalledOnceWith(
        'Generate Text',
        jasmine.objectContaining({ request, textToReplace: '' }),
      );
      expect(reopen).toHaveBeenCalledOnceWith({
        initialPromptId: 'prompt-1',
        initialModel: 'model-1',
        initialInstructions: 'continue it',
      });
    });

    it('inserts accepted generation output and persists editor HTML', async () => {
      const quill = setSelection();
      const close$ = new Subject<string>();
      proseGenerationDialogService.openTextResultDialog.and.returnValue(
        close$,
      );
      const emit = spyOn(component.proseChange, 'emit');

      component.openGenerateTextResultDialog(createRequest());
      close$.next('Generated');
      await flushAsyncSubscriber();

      expect(
        quill.clipboard.dangerouslyPasteHTML as jasmine.Spy,
      ).toHaveBeenCalledOnceWith(
        2,
        jasmine.stringContaining('Generated'),
        'user',
      );
      expect(component.prose.chapters[0].sections[0].text).toBe(
        '<p>Editor text</p>',
      );
      expect(emit).toHaveBeenCalledOnceWith(component.prose);
    });

    it('does not apply generation output after the selection is lost', () => {
      const close$ = new Subject<string>();
      proseGenerationDialogService.openTextResultDialog.and.returnValue(
        close$,
      );

      component.openGenerateTextResultDialog(createRequest());
      component.lastSelection = null;
      close$.next('Generated');

      expect(toastr.error).toHaveBeenCalledOnceWith(
        'Selection is no longer available.',
      );
    });

    it('opens replace flow, then replaces the original backend range', async () => {
      const quill = setSelection();
      component.prompts = [createPrompt(PromptType.ReplaceText)];
      const request$ = new Subject<GenerateTextRequestDto>();
      const result$ = new Subject<string>();
      proseGenerationDialogService.openTextRequestDialog.and.returnValue(
        request$,
      );
      proseGenerationDialogService.openTextResultDialog.and.returnValue(
        result$,
      );
      const request = createRequest(NovelTextGenerationType.ReplaceText);

      component.openReplaceTextDialog();
      const [header, replaceDialogData] =
        proseGenerationDialogService.openTextRequestDialog.calls.mostRecent()
          .args;
      expect(header).toBe('Replace Text');
      expect(replaceDialogData.contextInfo).toEqual(
        jasmine.objectContaining({ textOffset: 2, textLength: 3 }),
      );
      request$.next(request);
      result$.next('Replacement');
      await flushAsyncSubscriber();

      expect(
        proseGenerationDialogService.openTextResultDialog,
      ).toHaveBeenCalledOnceWith(
        'Replace Text',
        jasmine.objectContaining({ request, textToReplace: 'llo' }),
      );
      expect(quill.deleteText).toHaveBeenCalledOnceWith(2, 3);
      expect(
        quill.clipboard.dangerouslyPasteHTML as jasmine.Spy,
      ).toHaveBeenCalledOnceWith(
        2,
        jasmine.stringContaining('Replacement'),
        'user',
      );
    });

    it('opens summary generation and saves only on a complete update', () => {
      component.prompts = [createPrompt(PromptType.SummarizeText, 'summary')];
      const request$ = new Subject<GenerateTextRequestDto>();
      const updates$ = new Subject<GenerateTextStreamUpdate>();
      proseGenerationDialogService.openTextRequestDialog.and.returnValue(
        request$,
      );
      generateTextService.generateText.and.returnValue(updates$);
      const emit = spyOn(component.proseChange, 'emit');
      const request = createRequest();

      component.openGenerateSectionSummaryDialog(0, 0);
      request$.next(request);
      updates$.next({ content: 'Partial', isComplete: false });
      expect(component.prose.chapters[0].sections[0].summary).toBe('Partial');
      expect(emit).not.toHaveBeenCalled();

      updates$.next({ content: 'Final', isComplete: true });
      expect(component.prose.chapters[0].sections[0].summary).toBe('Final');
      expect(emit).toHaveBeenCalledOnceWith(component.prose);
    });

    it('validates story-suggestion selection and carries accepted suggestions forward', () => {
      const selection = setSelection(createQuill(), { index: 3, length: 1 });
      component.prompts = [
        createPrompt(PromptType.SuggestStoryDevelopments, 'suggest'),
      ];
      component.openGenerateStorySuggestionsDialog();
      expect(toastr.error).toHaveBeenCalledOnceWith(
        'Story suggestions are only available with no text selected.',
      );

      toastr.error.calls.reset();
      component.lastSelection = {
        ...component.lastSelection!,
        editor: selection,
        range: { index: 3, length: 0 },
      };
      const close$ = new Subject<{ model: string; instructions: string }>();
      proseGenerationDialogService.openStorySuggestionsDialog.and.returnValue(
        close$,
      );
      const generate = spyOn(component, 'openGenerateTextDialog');
      component.openGenerateStorySuggestionsDialog();
      close$.next({ model: 'story-model', instructions: 'Use option two' });

      expect(
        proseGenerationDialogService.openStorySuggestionsDialog,
      ).toHaveBeenCalledOnceWith(
        jasmine.objectContaining({
          prompts: [jasmine.objectContaining({ id: 'suggest' })],
          novelId: 'novel-1',
          chapterIndex: 0,
          sectionIndex: 0,
          textOffset: 3,
        }),
      );
      expect(generate).toHaveBeenCalledOnceWith({
        initialModel: 'story-model',
        initialInstructions: 'Use option two',
      });
    });

    it('runs the compendium-record dialog chain and emits when records changed', () => {
      setSelection();
      component.prompts = [createPrompt(PromptType.CreateCompendiumRecord)];
      const request$ = new Subject<GenerateTextRequestDto>();
      const generated$ = new Subject<string>();
      const changed$ = new Subject<boolean>();
      proseGenerationDialogService.openTextRequestDialog.and.returnValue(
        request$,
      );
      proseGenerationDialogService.openTextResultDialog.and.returnValue(
        generated$,
      );
      proseGenerationDialogService.openCompendiumRecordResultDialog.and
        .returnValue(changed$);
      const recordsEmit = spyOn(component.recordsChange, 'emit');

      component.openCreateCompendiumRecordDialog();
      request$.next(createRequest());
      generated$.next('{"name":"Ayla"}');
      changed$.next(true);

      expect(
        proseGenerationDialogService.openCompendiumRecordResultDialog,
      ).toHaveBeenCalledOnceWith(
        { generatedText: '{"name":"Ayla"}', novelId: 'novel-1' },
      );
      expect(recordsEmit).toHaveBeenCalledTimes(1);
    });
  });

  describe('images and overrides', () => {
    it('routes image-source choices to upload, generation, and clipboard actions', () => {
      const upload = spyOn(component, 'uploadProseImageFile');
      const generate = spyOn(component, 'generateProseImage');
      const clipboard = spyOn(component, 'uploadClipboardProseImage').and.resolveTo();

      for (const source of ['upload', 'generate', 'clipboard'] as const) {
        proseMediaService.selectSource.and.returnValue(of(source));
        component.addProseImage(0, 0);
      }

      expect(proseMediaService.selectSource).toHaveBeenCalledTimes(3);
      expect(upload).toHaveBeenCalledOnceWith(0, 0);
      expect(generate).toHaveBeenCalledOnceWith(0, 0);
      expect(clipboard).toHaveBeenCalledOnceWith(0, 0);
    });

    it('appends locations returned by file and generated-media uploads', () => {
      proseMediaService.selectFileAndUpload.and.returnValue(of('uploaded.png'));
      proseMediaService.generateAndUpload.and.returnValue(of('generated.mp4'));
      const emit = spyOn(component.proseChange, 'emit');

      component.uploadProseImageFile(0, 0);
      component.generateProseImage(0, 0);

      expect(proseMediaService.selectFileAndUpload).toHaveBeenCalledOnceWith(
        'novel-1',
      );
      expect(proseMediaService.generateAndUpload).toHaveBeenCalledOnceWith(
        'novel-1',
      );
      expect(component.prose.chapters[0].sections[0].images).toEqual([
        'uploaded.png',
        'generated.mp4',
      ]);
      expect(emit).toHaveBeenCalledTimes(2);
    });

    it('appends clipboard uploads and reports clipboard failures', async () => {
      proseMediaService.uploadClipboardImage.and.resolveTo('clipboard.png');
      const emit = spyOn(component.proseChange, 'emit');

      await component.uploadClipboardProseImage(0, 0);

      expect(component.prose.chapters[0].sections[0].images).toEqual([
        'clipboard.png',
      ]);
      expect(emit).toHaveBeenCalledOnceWith(component.prose);

      proseMediaService.uploadClipboardImage.and.rejectWith(
        new Error('No image found in the clipboard.'),
      );
      await component.uploadClipboardProseImage(0, 0);

      expect(toastr.error).toHaveBeenCalledWith(
        'No image found in the clipboard.',
      );
    });

    it('removes an image after server confirmation and keeps it after failure', () => {
      const section = component.prose.chapters[0].sections[0];
      section.images = ['one.png', 'two.png'];
      proseMediaService.deleteImage.and.returnValue(of(undefined));
      const emit = spyOn(component.proseChange, 'emit');

      component.removeProseImage(0, 0, 'one.png');
      let confirmation = confirmationService.confirm.calls.mostRecent()
        .args[0] as Confirmation;
      confirmation.accept?.();
      expect(section.images).toEqual(['two.png']);
      expect(emit).toHaveBeenCalledTimes(1);

      proseMediaService.deleteImage.and.returnValue(
        throwError(() => new Error('failed')),
      );
      component.removeProseImage(0, 0, 'two.png');
      confirmation = confirmationService.confirm.calls.mostRecent()
        .args[0] as Confirmation;
      confirmation.accept?.();
      expect(section.images).toEqual(['two.png']);
      expect(toastr.error).toHaveBeenCalledWith(
        'Could not remove image from the server.',
      );
    });

    it('opens record overrides with flattened records and saves returned overrides', () => {
      const section = component.prose.chapters[0].sections[0];
      const record = { id: 'record-1', name: 'Ayla' } as never;
      component.compendia = [{ records: [record] } as never];
      const close$ = new Subject<Section['recordOverrides']>();
      proseGenerationDialogService.openRecordOverridesDialog.and.returnValue(
        close$,
      );
      const emit = spyOn(component.proseChange, 'emit');
      const overrides = [
        {
          compendiumRecordId: 'record-1',
          keyword: 'name',
          description: 'Changed',
        },
      ];

      component.openRecordOverridesDialog(0, 0);
      expect(
        proseGenerationDialogService.openRecordOverridesDialog,
      ).toHaveBeenCalledOnceWith(
        jasmine.objectContaining({
          availableRecords: [record],
          prose: component.prose,
          chapterIndex: 0,
          sectionIndex: 0,
        }),
      );
      close$.next(overrides);

      expect(section.recordOverrides).toBe(overrides);
      expect(emit).toHaveBeenCalledOnceWith(component.prose);
    });
  });
});
