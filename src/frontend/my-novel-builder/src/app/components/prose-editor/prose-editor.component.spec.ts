import { TestBed } from '@angular/core/testing';
import { ConfirmationService } from 'primeng/api';
import type { Confirmation } from 'primeng/api';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { Subject, of, throwError } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import type {
  Blur,
  EditorChangeSelection,
  Range,
} from 'ngx-quill';
import type Quill from 'quill';
import { GenerateAudioService } from '../../services/generate-audio.service';
import { GenerateTextService } from '../../services/generate-text.service';
import type { GenerateTextStreamUpdate } from '../../services/generate-text.service';
import { IntegrationsService } from '../../services/integrations.service';
import { LocalStorageService } from '../../services/local-storage.service';
import { NovelService } from '../../services/novel.service';
import type {
  GenerateTextContextInfoDto,
  GenerateTextRequestDto,
  ReplaceTextContextInfoDto,
} from '../../types/dtos/generate/generate-text-request.dto';
import { NovelTextGenerationType } from '../../types/dtos/generate/generate-text-request.dto';
import type { Prose, Section } from '../../types/dtos/novel/prose';
import type { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { PromptType } from '../../types/enums/prompt-type';
import {
  STREAMING_WAV_PLAYER_FACTORY,
  StreamingWavPlayerFactory,
  StreamingWavPlayerHandle,
} from '../../utils/streaming-wav-player.factory';
import { GenerateCompendiumRecordResultComponent } from '../generate-compendium-record-result/generate-compendium-record-result.component';
import { GenerateStorySuggestionsDialogComponent } from '../generate-story-suggestions-dialog/generate-story-suggestions-dialog.component';
import { GenerateMediaComponent } from '../generate-media/generate-media.component';
import { GenerateTextResultComponent } from '../generate-text-result/generate-text-result.component';
import { GenerateTextComponent } from '../generate-text/generate-text.component';
import { ImageSourceSelectorComponent } from '../image-source-selector/image-source-selector.component';
import { RecordOverridesEditorComponent } from '../record-overrides-editor/record-overrides-editor.component';
import { ProseEditorComponent } from './prose-editor.component';

describe('ProseEditorComponent', () => {
  let component: ProseEditorComponent;
  let confirmationService: jasmine.SpyObj<ConfirmationService>;
  let dialogService: jasmine.SpyObj<DialogService>;
  let toastr: jasmine.SpyObj<ToastrService>;
  let generateTextService: jasmine.SpyObj<GenerateTextService>;
  let generateAudioService: jasmine.SpyObj<GenerateAudioService>;
  let novelService: jasmine.SpyObj<NovelService>;
  let localStorageService: jasmine.SpyObj<LocalStorageService>;
  let integrationsConfig$: Subject<{ ttsEnableImmersive: boolean }>;
  let player: jasmine.SpyObj<StreamingWavPlayerHandle>;
  let createPlayer: jasmine.Spy<StreamingWavPlayerFactory>;
  let firstAudioCallback: (() => void) | undefined;

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

  const createDialogRef = <T>(onClose = new Subject<T>()): DynamicDialogRef =>
    ({
      onClose,
      close: jasmine.createSpy('close'),
    }) as unknown as DynamicDialogRef;

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
    dialogService = jasmine.createSpyObj<DialogService>('DialogService', [
      'open',
    ]);
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
    generateAudioService = jasmine.createSpyObj<GenerateAudioService>(
      'GenerateAudioService',
      ['immersiveTextToSpeechStreamResponse', 'textToSpeechStreamResponse'],
    );
    novelService = jasmine.createSpyObj<NovelService>('NovelService', [
      'deleteProseImage',
      'uploadProseImage',
    ]);
    localStorageService = jasmine.createSpyObj<LocalStorageService>(
      'LocalStorageService',
      [
        'getNestedStringForKey',
        'removeNestedKey',
        'setNestedStringForKey',
      ],
    );
    integrationsConfig$ = new Subject<{ ttsEnableImmersive: boolean }>();
    player = jasmine.createSpyObj<StreamingWavPlayerHandle>(
      'StreamingWavPlayerHandle',
      ['addChunk', 'stop'],
    );
    createPlayer = jasmine.createSpy<StreamingWavPlayerFactory>(
      'StreamingWavPlayerFactory',
    ).and.callFake((callback) => {
      firstAudioCallback = callback;
      return player;
    });

    TestBed.configureTestingModule({
      providers: [
        { provide: ConfirmationService, useValue: confirmationService },
        { provide: DialogService, useValue: dialogService },
        { provide: ToastrService, useValue: toastr },
        { provide: GenerateTextService, useValue: generateTextService },
        { provide: GenerateAudioService, useValue: generateAudioService },
        { provide: NovelService, useValue: novelService },
        { provide: LocalStorageService, useValue: localStorageService },
        {
          provide: IntegrationsService,
          useValue: { getIntegrationsConfig: () => integrationsConfig$ },
        },
        { provide: STREAMING_WAV_PLAYER_FACTORY, useValue: createPlayer },
      ],
    });

    spyOn(console, 'error');
    spyOn(console, 'time');
    spyOn(console, 'timeEnd');

    component = TestBed.runInInjectionContext(() => new ProseEditorComponent());
    component.novelId = 'novel-1';
    component.prose = createProse();
    component.prompts = [];
    integrationsConfig$.next({ ttsEnableImmersive: false });
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
    it('derives input and send states from chapter, prompt, model, and content state', () => {
      component.selectedChapterIndex = 0;
      component.rpgPromptCount = 1;
      component.selectedRpgPromptId = 'prompt';
      component.selectedRpgModel = 'model';
      component.rpgInput = ' guide ';

      expect(component.isRpgInputDisabled()).toBeFalse();
      expect(component.getRpgInputPlaceholder()).toBe('Guide the next beat...');
      expect(component.isRpgSendDisabled()).toBeFalse();

      component.prose.chapters.push({
        title: 'Chapter 2',
        sections: [createSection()],
        storyEvents: [],
      });
      expect(component.isRpgInputDisabled()).toBeTrue();
      expect(component.getRpgInputPlaceholder()).toBe(
        'Go to the last chapter for RPG mode',
      );
      expect(component.isRpgSendDisabled()).toBeTrue();
    });

    it('updates RPG action and prompt-option count', () => {
      component.setRpgAction('say');
      component.onRpgPromptOptionsChanged(3);

      expect(component.rpgAction).toBe('say');
      expect(component.rpgPromptCount).toBe(3);
    });

    it('does nothing when sending is disabled', () => {
      component.sendRpgPrompt();

      expect(generateTextService.generateText).not.toHaveBeenCalled();
    });

    it('reports that a section is required before RPG generation', () => {
      component.prose.chapters[0].sections = [];
      component.rpgPromptCount = 1;
      component.selectedRpgPromptId = 'rpg-prompt';
      component.selectedRpgModel = 'rpg-model';
      component.rpgInput = 'Open the door';

      component.sendRpgPrompt();

      expect(toastr.error).toHaveBeenCalledOnceWith(
        'Add a section before using RPG mode.',
      );
      expect(generateTextService.generateText).not.toHaveBeenCalled();
    });

    it('appends completed RPG markdown without a mounted editor', async () => {
      component.rpgPromptCount = 1;
      component.selectedRpgPromptId = 'rpg-prompt';
      component.selectedRpgModel = 'rpg-model';
      component.rpgAction = 'say';
      component.rpgInput = '  Hello there  ';
      generateTextService.generateText.and.returnValue(
        of({ content: '**Reply**', isComplete: true }),
      );
      const append = spyOn<any>(component, 'appendMarkdownToHtml').and.resolveTo(
        '<p>Text</p><p><strong>Reply</strong></p>',
      );
      const emit = spyOn(component.proseChange, 'emit');

      component.sendRpgPrompt();
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
      expect(append).toHaveBeenCalledOnceWith('<p>Text</p>', '**Reply**');
      expect(component.prose.chapters[0].sections[0].text).toContain('Reply');
      expect(component.rpgInput).toBe('');
      expect(component.isRpgGenerating).toBeFalse();
      expect(emit).toHaveBeenCalledTimes(2);
    });

    it('appends RPG output to a mounted editor at its final offset', async () => {
      const quill = createQuill();
      component.editorInit(quill, 0, 0);
      component.rpgPromptCount = 1;
      component.selectedRpgPromptId = 'rpg-prompt';
      component.selectedRpgModel = 'rpg-model';
      component.rpgInput = 'continue';
      generateTextService.generateText.and.returnValue(
        of({ content: 'Next', isComplete: true }),
      );
      const insert = spyOn<any>(component, 'insertGeneratedMarkdown').and.resolveTo();

      component.sendRpgPrompt();
      await flushAsyncSubscriber();

      expect(insert).toHaveBeenCalledOnceWith(quill, 5, 'Next');
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
      component.rpgPromptCount = 1;
      component.selectedRpgPromptId = 'rpg-prompt';
      component.selectedRpgModel = 'rpg-model';
      component.rpgInput = 'first';
      generateTextService.generateText.and.returnValue(
        of({ content: '   ', isComplete: true }),
      );

      component.sendRpgPrompt();
      expect(toastr.error).toHaveBeenCalledWith('No RPG response was generated.');
      expect(component.isRpgGenerating).toBeFalse();

      toastr.error.calls.reset();
      component.rpgInput = ' retry this ';
      generateTextService.generateText.and.returnValue(
        throwError(() => new Error('network')),
      );
      component.sendRpgPrompt();

      expect(component.rpgInput).toBe('retry this');
      expect(component.isRpgGenerating).toBeFalse();
      expect(toastr.error).toHaveBeenCalledOnceWith(
        'Failed to generate RPG response.',
      );
    });
  });

  describe('text to speech', () => {
    const playableResponse = () => new Response(new Uint8Array(45));

    it('streams narrator audio, strips markup, and clears loading on first audio', async () => {
      component.prose.chapters[0].sections[0].text =
        '<p>Hello</p><p>world</p>';
      generateAudioService.textToSpeechStreamResponse.and.resolveTo(
        playableResponse(),
      );
      player.addChunk.and.callFake(() => firstAudioCallback?.());

      await component.textToSpeech(0, 0);

      expect(generateAudioService.textToSpeechStreamResponse)
        .toHaveBeenCalledOnceWith({ message: 'Hello world' });
      expect(createPlayer).toHaveBeenCalledTimes(1);
      expect(player.addChunk).toHaveBeenCalled();
      expect(toastr.info).toHaveBeenCalledWith(
        'Generating TTS...',
        '',
        jasmine.objectContaining({ timeOut: 0, tapToDismiss: false }),
      );
      expect(toastr.clear).toHaveBeenCalledOnceWith(41);
    });

    it('uses the stored immersive prompt when it remains available', async () => {
      component.prompts = [
        createPrompt(PromptType.PrepareImmersiveTts, 'immersive-1'),
      ];
      integrationsConfig$.next({ ttsEnableImmersive: true });
      localStorageService.getNestedStringForKey.and.returnValue('immersive-1');
      generateAudioService.immersiveTextToSpeechStreamResponse.and.resolveTo(
        playableResponse(),
      );

      await component.textToSpeech(0, 0);

      expect(
        generateAudioService.immersiveTextToSpeechStreamResponse,
      ).toHaveBeenCalledOnceWith({
        novelId: 'novel-1',
        promptId: 'immersive-1',
        chapterIndex: 0,
        sectionIndex: 0,
      });
      expect(generateAudioService.textToSpeechStreamResponse).not.toHaveBeenCalled();
    });

    it('replaces a stale immersive preference with the first available prompt', async () => {
      component.prompts = [
        createPrompt(PromptType.PrepareImmersiveTts, 'fallback'),
      ];
      integrationsConfig$.next({ ttsEnableImmersive: true });
      localStorageService.getNestedStringForKey.and.returnValue('removed');
      generateAudioService.immersiveTextToSpeechStreamResponse.and.resolveTo(
        playableResponse(),
      );

      await component.textToSpeech(0, 0);

      expect(localStorageService.removeNestedKey).toHaveBeenCalledOnceWith(
        LocalStorageKey.RecentPrompts,
        PromptType.PrepareImmersiveTts,
      );
      expect(localStorageService.setNestedStringForKey).toHaveBeenCalledOnceWith(
        LocalStorageKey.RecentPrompts,
        PromptType.PrepareImmersiveTts,
        'fallback',
      );
    });

    it('falls back to narrator audio when immersive playback fails', async () => {
      component.prompts = [
        createPrompt(PromptType.PrepareImmersiveTts, 'immersive'),
      ];
      integrationsConfig$.next({ ttsEnableImmersive: true });
      generateAudioService.immersiveTextToSpeechStreamResponse.and.rejectWith(
        new Error('provider unavailable'),
      );
      generateAudioService.textToSpeechStreamResponse.and.resolveTo(
        playableResponse(),
      );

      await component.textToSpeech(0, 0);

      expect(toastr.warning).toHaveBeenCalledOnceWith(
        'Immersive TTS failed (provider unavailable). Falling back to narrator-only playback.',
      );
      expect(generateAudioService.textToSpeechStreamResponse).toHaveBeenCalled();
    });

    it('explains the fallback when immersive mode has no configured prompt', async () => {
      integrationsConfig$.next({ ttsEnableImmersive: true });
      generateAudioService.textToSpeechStreamResponse.and.resolveTo(
        playableResponse(),
      );

      await component.textToSpeech(0, 0);

      expect(toastr.info).toHaveBeenCalledWith(
        'No immersive TTS prompt is configured. Falling back to narrator-only playback.',
      );
    });

    it('reports a missing response body and rejects header-only audio', async () => {
      generateAudioService.textToSpeechStreamResponse.and.resolveTo(
        new Response(null),
      );
      await component.textToSpeech(0, 0);
      expect(toastr.error).toHaveBeenCalledOnceWith(
        'No audio stream was returned.',
      );

      toastr.error.calls.reset();
      generateAudioService.textToSpeechStreamResponse.and.resolveTo(
        new Response(new Uint8Array(44)),
      );
      await component.textToSpeech(0, 0);

      expect(console.error).toHaveBeenCalledWith(
        'WAV streaming error:',
        jasmine.any(Error),
      );
      expect(toastr.clear).toHaveBeenCalledWith(41);
    });
  });

  describe('generation dialogs', () => {
    it('converts markdown and inserts it into Quill as user content', async () => {
      const quill = createQuill();

      await (component as any).insertGeneratedMarkdown(
        quill,
        4,
        '**Generated**',
      );
      const appended = await (component as any).appendMarkdownToHtml(
        '<p>Existing</p>',
        '_Next_',
      );

      expect(quill.clipboard.dangerouslyPasteHTML).toHaveBeenCalledWith(
        4,
        jasmine.stringContaining('<strong>Generated</strong>'),
        'user',
      );
      expect(appended).toContain('<p><em>Next</em></p>');
    });

    it('requires a matching summary prompt', () => {
      component.openGenerateSectionSummaryDialog(0, 0);

      expect(toastr.error).toHaveBeenCalledOnceWith(
        'No summarization prompts available',
      );
      expect(dialogService.open).not.toHaveBeenCalled();
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
      expect(dialogService.open).not.toHaveBeenCalled();
    });

    it('opens the generate-text dialog with selection context and prefill', () => {
      setSelection();
      component.prompts = [
        createPrompt(PromptType.GenerateText, 'generate'),
        createPrompt(PromptType.ReplaceText, 'replace'),
      ];
      const close$ = new Subject<GenerateTextRequestDto>();
      dialogService.open.and.returnValue(createDialogRef(close$));
      const openResult = spyOn(component, 'openGenerateTextResultDialog');
      const emit = spyOn(component.proseChange, 'emit');
      const request = createRequest();

      component.openGenerateTextDialog({
        initialPromptId: 'recent-prompt',
        initialModel: 'recent-model',
        initialInstructions: 'keep going',
      });
      close$.next(request);

      const [dialog, config] = dialogService.open.calls.mostRecent().args;
      expect(dialog).toBe(GenerateTextComponent);
      expect(config?.data).toEqual(
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
      dialogService.open.and.returnValue(createDialogRef(close$));
      const reopen = spyOn(component, 'openGenerateTextDialog');
      const request = createRequest();

      component.openGenerateTextResultDialog(request);
      close$.next('back');

      expect(dialogService.open.calls.mostRecent().args[0]).toBe(
        GenerateTextResultComponent,
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
      dialogService.open.and.returnValue(createDialogRef(close$));
      const insert = spyOn<any>(component, 'insertGeneratedMarkdown').and.resolveTo();
      const emit = spyOn(component.proseChange, 'emit');

      component.openGenerateTextResultDialog(createRequest());
      close$.next('Generated');
      await flushAsyncSubscriber();

      expect(insert).toHaveBeenCalledOnceWith(quill, 2, 'Generated');
      expect(component.prose.chapters[0].sections[0].text).toBe(
        '<p>Editor text</p>',
      );
      expect(emit).toHaveBeenCalledOnceWith(component.prose);
    });

    it('does not apply generation output after the selection is lost', () => {
      const close$ = new Subject<string>();
      dialogService.open.and.returnValue(createDialogRef(close$));

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
      dialogService.open.and.returnValues(
        createDialogRef(request$),
        createDialogRef(result$),
      );
      const insert = spyOn<any>(component, 'insertGeneratedMarkdown').and.resolveTo();
      const request = createRequest(NovelTextGenerationType.ReplaceText);

      component.openReplaceTextDialog();
      expect(dialogService.open.calls.argsFor(0)[0]).toBe(GenerateTextComponent);
      const replaceDialogData = dialogService.open.calls.argsFor(0)[1]
        ?.data as { contextInfo: ReplaceTextContextInfoDto };
      expect(replaceDialogData.contextInfo).toEqual(
        jasmine.objectContaining({ textOffset: 2, textLength: 3 }),
      );
      request$.next(request);
      result$.next('Replacement');
      await flushAsyncSubscriber();

      expect(dialogService.open.calls.argsFor(1)[0]).toBe(
        GenerateTextResultComponent,
      );
      expect(quill.deleteText).toHaveBeenCalledOnceWith(2, 3);
      expect(insert).toHaveBeenCalledOnceWith(quill, 2, 'Replacement');
    });

    it('opens summary generation and saves only on a complete update', () => {
      component.prompts = [createPrompt(PromptType.SummarizeText, 'summary')];
      const request$ = new Subject<GenerateTextRequestDto>();
      const updates$ = new Subject<GenerateTextStreamUpdate>();
      dialogService.open.and.returnValue(createDialogRef(request$));
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
      dialogService.open.and.returnValue(createDialogRef(close$));
      const generate = spyOn(component, 'openGenerateTextDialog');
      component.openGenerateStorySuggestionsDialog();
      close$.next({ model: 'story-model', instructions: 'Use option two' });

      expect(dialogService.open.calls.mostRecent().args[0]).toBe(
        GenerateStorySuggestionsDialogComponent,
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
      dialogService.open.and.returnValues(
        createDialogRef(request$),
        createDialogRef(generated$),
        createDialogRef(changed$),
      );
      const recordsEmit = spyOn(component.recordsChange, 'emit');

      component.openCreateCompendiumRecordDialog();
      request$.next(createRequest());
      generated$.next('{"name":"Ayla"}');
      changed$.next(true);

      expect(dialogService.open.calls.argsFor(2)[0]).toBe(
        GenerateCompendiumRecordResultComponent,
      );
      expect(recordsEmit).toHaveBeenCalledTimes(1);
    });
  });

  describe('images, overrides, and cleanup', () => {
    it('routes image-source choices to upload, generation, and clipboard actions', () => {
      const upload = spyOn(component, 'uploadProseImageFile');
      const generate = spyOn(component, 'generateProseImage');
      const clipboard = spyOn(component, 'uploadClipboardProseImage').and.resolveTo();

      for (const result of ['upload', 'generate', 'clipboard'] as const) {
        const close$ = new Subject<typeof result>();
        dialogService.open.and.returnValue(createDialogRef(close$));
        component.addProseImage(0, 0);
        expect(dialogService.open.calls.mostRecent().args[0]).toBe(
          ImageSourceSelectorComponent,
        );
        close$.next(result);
      }

      expect(upload).toHaveBeenCalledOnceWith(0, 0);
      expect(generate).toHaveBeenCalledOnceWith(0, 0);
      expect(clipboard).toHaveBeenCalledOnceWith(0, 0);
    });

    it('uploads a selected image file and appends the returned location', () => {
      const input = document.createElement('input');
      const file = new File(['image'], 'scene.png', { type: 'image/png' });
      Object.defineProperty(input, 'files', { value: [file] });
      spyOn(input, 'click');
      spyOn(input, 'remove');
      spyOn(document, 'createElement').and.returnValue(input);
      novelService.uploadProseImage.and.returnValue(of('uploaded.png'));
      const emit = spyOn(component.proseChange, 'emit');

      component.uploadProseImageFile(0, 0);
      input.onchange?.(new Event('change'));

      expect(input.type).toBe('file');
      expect(input.accept).toBe('image/*,video/*');
      expect(input.click).toHaveBeenCalled();
      expect(novelService.uploadProseImage).toHaveBeenCalledOnceWith(
        'novel-1',
        file,
      );
      expect(component.prose.chapters[0].sections[0].images).toEqual([
        'uploaded.png',
      ]);
      expect(emit).toHaveBeenCalledOnceWith(component.prose);
      expect(input.remove).toHaveBeenCalled();
    });

    it('uploads generated media with a filename derived from its MIME type', () => {
      const close$ = new Subject<Blob>();
      dialogService.open.and.returnValue(createDialogRef(close$));
      novelService.uploadProseImage.and.returnValue(of('generated.mp4'));
      const emit = spyOn(component.proseChange, 'emit');
      const video = new Blob(['video'], { type: 'video/mp4' });

      component.generateProseImage(0, 0);
      close$.next(video);

      expect(dialogService.open.calls.mostRecent().args[0]).toBe(
        GenerateMediaComponent,
      );
      const uploadedFile = novelService.uploadProseImage.calls.mostRecent()
        .args[1];
      expect(uploadedFile.name).toBe('generated-media.mp4');
      expect(uploadedFile.type).toBe('video/mp4');
      expect(component.prose.chapters[0].sections[0].images).toEqual([
        'generated.mp4',
      ]);
      expect(emit).toHaveBeenCalledOnceWith(component.prose);
    });

    it('removes an image after server confirmation and keeps it after failure', () => {
      const section = component.prose.chapters[0].sections[0];
      section.images = ['one.png', 'two.png'];
      novelService.deleteProseImage.and.returnValue(of(undefined));
      const emit = spyOn(component.proseChange, 'emit');

      component.removeProseImage(0, 0, 'one.png');
      let confirmation = confirmationService.confirm.calls.mostRecent()
        .args[0] as Confirmation;
      confirmation.accept?.();
      expect(section.images).toEqual(['two.png']);
      expect(emit).toHaveBeenCalledTimes(1);

      novelService.deleteProseImage.and.returnValue(
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
      dialogService.open.and.returnValue(createDialogRef(close$));
      const emit = spyOn(component.proseChange, 'emit');
      const overrides = [
        {
          compendiumRecordId: 'record-1',
          keyword: 'name',
          description: 'Changed',
        },
      ];

      component.openRecordOverridesDialog(0, 0);
      expect(dialogService.open.calls.mostRecent().args[0]).toBe(
        RecordOverridesEditorComponent,
      );
      const overridesDialogData = dialogService.open.calls.mostRecent().args[1]
        ?.data as { availableRecords: unknown[] };
      expect(overridesDialogData.availableRecords).toEqual([record]);
      close$.next(overrides);

      expect(section.recordOverrides).toBe(overrides);
      expect(emit).toHaveBeenCalledOnceWith(component.prose);
    });

    it('closes the active dialog and clears an outstanding TTS toast on destroy', () => {
      const ref = createDialogRef();
      dialogService.open.and.returnValue(ref);
      component.addProseImage(0, 0);
      (component as unknown as { ttsLoadingToastId?: number }).ttsLoadingToastId =
        99;

      component.ngOnDestroy();

      expect(ref.close).toHaveBeenCalled();
      expect(toastr.clear).toHaveBeenCalledOnceWith(99);
    });

    it('falls back safely when integrations configuration cannot be loaded', () => {
      integrationsConfig$.error(new Error('config failed'));

      expect(console.error).toHaveBeenCalledWith(
        'Error loading integrations config for TTS:',
        jasmine.any(Error),
      );
      expect(
        (component as unknown as { ttsEnableImmersive: boolean })
          .ttsEnableImmersive,
      ).toBeFalse();
    });
  });
});
