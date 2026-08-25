import {
  SimpleChange,
  SimpleChanges,
} from '@angular/core';
import { fakeAsync, flushMicrotasks, TestBed } from '@angular/core/testing';
import { ConfirmationService } from 'primeng/api';
import {
  DialogService,
  DynamicDialogRef,
} from 'primeng/dynamicdialog';
import { ToastrService } from 'ngx-toastr';
import { of, Subject, throwError } from 'rxjs';
import { ChatService } from '../../services/chat.service';
import { CompendiumService } from '../../services/compendium.service';
import {
  GenerateTextService,
  GenerateTextStreamUpdate,
} from '../../services/generate-text.service';
import { LocalStorageService } from '../../services/local-storage.service';
import { NovelService } from '../../services/novel.service';
import { Chat, ChatMessage } from '../../types/dtos/chats/chat';
import { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import {
  GenerateTextRequestDto,
  NovelTextGenerationType,
  SendChatMessageContextInfoDto,
} from '../../types/dtos/generate/generate-text-request.dto';
import { NovelDto } from '../../types/dtos/novel/novel.dto';
import { Prose } from '../../types/dtos/novel/prose';
import { ChatMessageRole } from '../../types/enums/chat-message-role';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { PromptType } from '../../types/enums/prompt-type';
import { WritingLanguage } from '../../types/enums/writing-language';
import { WritingPov } from '../../types/enums/writing-pov';
import { WritingTense } from '../../types/enums/writing-tense';
import { EditChatMessageComponent } from '../edit-chat-message/edit-chat-message.component';
import { GenerateTextPreviewDialogService } from '../generate-text-preview/generate-text-preview-dialog.service';
import { ChatComponent } from './chat.component';

describe('ChatComponent workflow', () => {
  let component: ChatComponent;
  let chatService: jasmine.SpyObj<ChatService>;
  let novelService: jasmine.SpyObj<NovelService>;
  let compendiumService: jasmine.SpyObj<CompendiumService>;
  let confirmationService: jasmine.SpyObj<ConfirmationService>;
  let dialogService: jasmine.SpyObj<DialogService>;
  let generateTextService: jasmine.SpyObj<GenerateTextService>;
  let previewDialogService: jasmine.SpyObj<GenerateTextPreviewDialogService>;
  let localStorageService: jasmine.SpyObj<LocalStorageService>;
  let toastr: jasmine.SpyObj<ToastrService>;
  let generation: Subject<GenerateTextStreamUpdate>;
  let dialogClose: Subject<string | undefined>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let originalClipboardDescriptor: PropertyDescriptor | undefined;

  const novel = (id = 'novel-1'): NovelDto => ({
    id,
    createdAt: '2026-07-29T12:00:00Z',
    updatedAt: '2026-07-29T12:00:00Z',
    title: `Novel ${id}`,
    author: '',
    brief: '',
    coverImageUrl: null,
    tense: WritingTense.Present,
    pov: WritingPov.FirstPerson,
    language: WritingLanguage.English,
    rpgMode: false,
    mainCharacterId: null,
    compendiumIds: ['compendium-1'],
  });

  const prose = (title = 'Chapter one'): Prose => ({
    chapters: [
      {
        title,
        sections: [],
        storyEvents: [],
      },
    ],
  });

  const compendium = (
    id: string,
    recordIds: string[],
  ): CompendiumDto => ({
    id,
    createdAt: '2026-07-29T12:00:00Z',
    updatedAt: '2026-07-29T12:00:00Z',
    name: `Compendium ${id}`,
    description: '',
    records: recordIds.map((recordId) => ({
      id: recordId,
      name: `Record ${recordId}`,
      type: CompendiumRecordType.Character,
      imageUrl: null,
    })),
  });

  const message = (
    id: string,
    role: ChatMessageRole,
    textContent: string,
  ): ChatMessage => ({
    id,
    role,
    textContent,
    sentAt: '2026-07-29T12:00:00Z',
  });

  const chat = (novelId = 'novel-1'): Chat => ({
    id: 'chat-1',
    createdAt: '2026-07-29T12:00:00Z',
    updatedAt: '2026-07-29T12:00:00Z',
    name: 'Chat',
    context: {
      novelId,
      chapterIndex: null,
      compendiumIds: [],
      compendiumRecordIds: [],
    },
    messages: [],
  });

  const currentChatChange = (firstChange = true): SimpleChanges => ({
    currentChat: new SimpleChange(
      firstChange ? undefined : chat('previous-novel'),
      component.currentChat,
      firstChange,
    ),
  });

  const selectGenerationOptions = (): void => {
    component.selectedModel = 'model-a';
    component.selectedPromptId = 'prompt-a';
  };

  beforeEach(() => {
    originalClipboardDescriptor = Object.getOwnPropertyDescriptor(
      navigator,
      'clipboard',
    );
    generation = new Subject<GenerateTextStreamUpdate>();
    dialogClose = new Subject<string | undefined>();
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>(
      'DynamicDialogRef',
      ['close'],
      { onClose: dialogClose },
    );
    chatService = jasmine.createSpyObj<ChatService>('ChatService', [
      'updateChat',
    ]);
    novelService = jasmine.createSpyObj<NovelService>('NovelService', [
      'getNovel',
      'getNovelProse',
    ]);
    compendiumService = jasmine.createSpyObj<CompendiumService>(
      'CompendiumService',
      ['getCompendia'],
    );
    confirmationService = jasmine.createSpyObj<ConfirmationService>(
      'ConfirmationService',
      ['confirm'],
    );
    dialogService = jasmine.createSpyObj<DialogService>('DialogService', [
      'open',
    ]);
    generateTextService = jasmine.createSpyObj<GenerateTextService>(
      'GenerateTextService',
      ['generateText'],
    );
    previewDialogService =
      jasmine.createSpyObj<GenerateTextPreviewDialogService>(
        'GenerateTextPreviewDialogService',
        ['open'],
      );
    localStorageService = jasmine.createSpyObj<LocalStorageService>(
      'LocalStorageService',
      ['setNestedStringForKey'],
    );
    toastr = jasmine.createSpyObj<ToastrService>('ToastrService', [
      'success',
      'error',
    ]);

    chatService.updateChat.and.returnValue(of(undefined));
    novelService.getNovel.and.returnValue(of(novel()));
    novelService.getNovelProse.and.returnValue(of(prose()));
    compendiumService.getCompendia.and.returnValue(
      of([
        compendium('compendium-1', ['record-1']),
        compendium('compendium-2', ['record-2']),
      ]),
    );
    generateTextService.generateText.and.returnValue(generation);
    previewDialogService.open.and.returnValue(dialogRef);
    dialogService.open.and.returnValue(dialogRef);

    TestBed.configureTestingModule({
      providers: [
        { provide: ChatService, useValue: chatService },
        { provide: NovelService, useValue: novelService },
        { provide: CompendiumService, useValue: compendiumService },
        { provide: ConfirmationService, useValue: confirmationService },
        { provide: DialogService, useValue: dialogService },
        { provide: GenerateTextService, useValue: generateTextService },
        {
          provide: GenerateTextPreviewDialogService,
          useValue: previewDialogService,
        },
        { provide: LocalStorageService, useValue: localStorageService },
        { provide: ToastrService, useValue: toastr },
      ],
    });

    component = TestBed.runInInjectionContext(() => new ChatComponent());
    component.currentChatId = 'chat-1';
    component.currentChat = chat();
    spyOn(component.onChatUpdated, 'emit');
  });

  afterEach(() => {
    component.ngOnDestroy();
    if (originalClipboardDescriptor === undefined) {
      delete (navigator as unknown as { clipboard?: Clipboard }).clipboard;
    } else {
      Object.defineProperty(
        navigator,
        'clipboard',
        originalClipboardDescriptor,
      );
    }
  });

  it('derives chapter and record options from the loaded context', () => {
    component.prose.set({
      chapters: [
        { title: 'One', sections: [], storyEvents: [] },
        { title: 'Two', sections: [], storyEvents: [] },
      ],
    });
    component.compendia.set([
      compendium('compendium-1', ['record-1']),
      compendium('compendium-2', ['record-2']),
    ]);

    expect(component.chapters()).toEqual([
      { label: 'One', value: 0 },
      { label: 'Two', value: 1 },
    ]);
    expect(component.allAvailableRecords().map((record) => record.id)).toEqual([
      'record-1',
      'record-2',
    ]);

    component.prose.set(null);
    component.compendia.set(null);
    expect(component.chapters()).toEqual([]);
    expect(component.allAvailableRecords()).toEqual([]);
  });

  it('loads the selected novel, prose, and only its linked compendia', () => {
    component.ngOnChanges(currentChatChange());

    expect(novelService.getNovel).toHaveBeenCalledOnceWith('novel-1');
    expect(novelService.getNovelProse).toHaveBeenCalledOnceWith('novel-1');
    expect(component.novel()).toEqual(novel());
    expect(component.prose()).toEqual(prose());
    expect(component.compendia()?.map((item) => item.id)).toEqual([
      'compendium-1',
    ]);
    expect(component.novelNotFound()).toBeFalse();
  });

  it('shows the missing-novel state when loading the novel fails', () => {
    novelService.getNovel.and.returnValue(
      throwError(() => new Error('not found')),
    );

    component.ngOnChanges(currentChatChange());

    expect(component.novel()).toBeNull();
    expect(component.novelNotFound()).toBeTrue();
    expect(compendiumService.getCompendia).not.toHaveBeenCalled();
  });

  it('keeps optional context empty when prose or compendia loading fails', () => {
    novelService.getNovelProse.and.returnValue(
      throwError(() => new Error('prose failed')),
    );
    compendiumService.getCompendia.and.returnValue(
      throwError(() => new Error('compendia failed')),
    );

    expect(() => component.ngOnChanges(currentChatChange())).not.toThrow();

    expect(component.novel()).toEqual(novel());
    expect(component.prose()).toBeNull();
    expect(component.compendia()).toBeNull();
    expect(component.novelNotFound()).toBeFalse();
  });

  it('cancels stale context requests when the current chat changes', () => {
    const firstNovel = new Subject<NovelDto>();
    const firstProse = new Subject<Prose>();
    novelService.getNovel.and.returnValue(firstNovel);
    novelService.getNovelProse.and.returnValue(firstProse);
    component.ngOnChanges(currentChatChange());

    component.currentChat = chat('novel-2');
    novelService.getNovel.and.returnValue(of(novel('novel-2')));
    novelService.getNovelProse.and.returnValue(of(prose('New chapter')));
    component.ngOnChanges(currentChatChange(false));
    firstNovel.next(novel('stale-novel'));
    firstProse.next(prose('Stale chapter'));

    expect(component.novel()?.id).toBe('novel-2');
    expect(component.prose()?.chapters[0].title).toBe('New chapter');
  });

  it('scrolls once after a change requests it', () => {
    const nativeElement = { scrollTop: 0, scrollHeight: 420 };
    component['chatContainer'] = { nativeElement } as never;
    component.ngOnChanges(currentChatChange());

    component.ngAfterViewChecked();
    expect(nativeElement.scrollTop).toBe(420);

    nativeElement.scrollHeight = 800;
    component.ngAfterViewChecked();
    expect(nativeElement.scrollTop).toBe(420);
  });

  it('handles a missing chat element without breaking change detection', () => {
    const consoleError = spyOn(console, 'error');
    component.ngOnChanges(currentChatChange());

    expect(() => component.ngAfterViewChecked()).not.toThrow();
    expect(consoleError).toHaveBeenCalled();
  });

  it('updates the chat name and persists the complete chat state', () => {
    component.currentChat.context.chapterIndex = 2;
    component.currentChat.context.compendiumIds = ['compendium-1'];
    component.currentChat.context.compendiumRecordIds = ['record-1'];
    component.currentChat.messages = [
      message('message-1', ChatMessageRole.User, 'Hello'),
    ];

    component.updateChatName('Renamed chat');

    expect(component.currentChat.name).toBe('Renamed chat');
    expect(chatService.updateChat).toHaveBeenCalledOnceWith('chat-1', {
      name: 'Renamed chat',
      chapterIndex: 2,
      compendiumIds: ['compendium-1'],
      compendiumRecordIds: ['record-1'],
      messages: component.currentChat.messages,
    });
    expect(component.onChatUpdated.emit).toHaveBeenCalledTimes(1);
  });

  it('updates and persists a selected or cleared chapter', () => {
    component.updateChatChapter(3);
    expect(component.currentChat.context.chapterIndex).toBe(3);

    component.updateChatChapter(null);
    expect(component.currentChat.context.chapterIndex).toBeNull();
    expect(chatService.updateChat).toHaveBeenCalledTimes(2);
  });

  it('selects every record when a compendium is newly selected', () => {
    component.compendia.set([
      compendium('compendium-1', ['record-1', 'shared-record']),
      compendium('compendium-2', ['shared-record', 'record-2']),
    ]);
    component.currentChat.context.compendiumIds = ['compendium-1'];
    component.currentChat.context.compendiumRecordIds = [
      'record-1',
      'shared-record',
    ];

    component.onCompendiaChange({
      value: ['compendium-1', 'compendium-2'],
    });

    expect(component.currentChat.context.compendiumIds).toEqual([
      'compendium-1',
      'compendium-2',
    ]);
    expect(component.currentChat.context.compendiumRecordIds).toEqual([
      'record-1',
      'shared-record',
      'record-2',
    ]);
    expect(chatService.updateChat).toHaveBeenCalledTimes(1);
  });

  it('synchronizes compendium selections from complete record selections', () => {
    component.compendia.set([
      compendium('compendium-1', ['record-1', 'record-2']),
      compendium('empty-compendium', []),
    ]);

    component.onRecordsChange({ value: ['record-1', 'record-2'] });
    expect(component.currentChat.context.compendiumIds).toEqual([
      'compendium-1',
    ]);

    component.onRecordsChange({ value: ['record-1'] });
    expect(component.currentChat.context.compendiumIds).toEqual([]);
    expect(
      component.currentChat.context.compendiumIds,
    ).not.toContain('empty-compendium');
  });

  it('tracks the available prompt count', () => {
    component.onPromptOptionsChanged(4);
    expect(component.promptCount).toBe(4);

    component.onPromptOptionsChanged(0);
    expect(component.promptCount).toBe(0);
  });

  it('only allows resending an unmatched user message while idle', () => {
    const firstUser = message('user-1', ChatMessageRole.User, 'First');
    const assistant = message(
      'assistant-1',
      ChatMessageRole.Assistant,
      'Reply',
    );
    const lastUser = message('user-2', ChatMessageRole.User, 'Second');
    component.currentChat.messages = [firstUser, assistant, lastUser];

    expect(component.canResend(firstUser)).toBeFalse();
    expect(component.canResend(assistant)).toBeFalse();
    expect(component.canResend(lastUser)).toBeTrue();

    component.isGenerating = true;
    expect(component.canResend(lastUser)).toBeFalse();
  });

  it('ignores sending when input or generation options are invalid', () => {
    component.userInput = '   ';
    component.sendMessage();

    component.userInput = 'Hello';
    component.selectedModel = 'model-a';
    component.sendMessage();

    component.selectedModel = null;
    component.selectedPromptId = 'prompt-a';
    component.sendMessage();

    component.selectedModel = 'model-a';
    component.isGenerating = true;
    component.sendMessage();

    expect(component.currentChat.messages).toEqual([]);
    expect(generateTextService.generateText).not.toHaveBeenCalled();
  });

  it('does not preview when input or generation options are invalid', () => {
    component.userInput = 'Message';

    component.previewMessage();

    expect(previewDialogService.open).not.toHaveBeenCalled();
    expect(component.currentChat.messages).toEqual([]);
  });

  it('previews the exact request without changing the conversation', () => {
    component.currentChat.context = {
      novelId: 'novel-1',
      chapterIndex: 2,
      compendiumIds: ['compendium-1'],
      compendiumRecordIds: ['record-1'],
    };
    component.currentChat.messages = [
      message('old-user', ChatMessageRole.User, 'Earlier question'),
      message('old-assistant', ChatMessageRole.Assistant, 'Earlier answer'),
    ];
    component.userInput = 'New question';
    selectGenerationOptions();

    component.previewMessage();

    const expectedContext: SendChatMessageContextInfoDto = {
      $type: NovelTextGenerationType.SendChatMessage,
      novelId: 'novel-1',
      chapterIndex: 2,
      userMessage: 'New question',
      previousMessages: [
        { role: ChatMessageRole.User, textContent: 'Earlier question' },
        {
          role: ChatMessageRole.Assistant,
          textContent: 'Earlier answer',
        },
      ],
      compendiumIds: ['compendium-1'],
      compendiumRecordIds: ['record-1'],
    };
    const expectedRequest: GenerateTextRequestDto = {
      model: 'model-a',
      promptId: 'prompt-a',
      contextInfo: expectedContext,
    };
    expect(previewDialogService.open).toHaveBeenCalledOnceWith(
      expectedRequest,
    );
    expect(component.currentChat.messages.length).toBe(2);
    expect(component.userInput).toBe('New question');
    expect(generateTextService.generateText).not.toHaveBeenCalled();
  });

  it('adds both messages and creates the generation request with prior history', () => {
    component.currentChat.context = {
      novelId: 'novel-1',
      chapterIndex: 2,
      compendiumIds: ['compendium-1'],
      compendiumRecordIds: ['record-1'],
    };
    component.currentChat.messages = [
      message('old-user', ChatMessageRole.User, 'Earlier question'),
      message('old-assistant', ChatMessageRole.Assistant, 'Earlier answer'),
    ];
    component.userInput = 'New question';
    selectGenerationOptions();

    component.sendMessage();

    expect(component.userInput).toBe('');
    expect(component.currentChat.messages.length).toBe(4);
    expect(component.currentChat.messages[2]).toEqual(
      jasmine.objectContaining({
        role: ChatMessageRole.User,
        textContent: 'New question',
      }),
    );
    expect(component.currentChat.messages[3]).toEqual(
      jasmine.objectContaining({
        role: ChatMessageRole.Assistant,
        textContent: '',
      }),
    );
    expect(localStorageService.setNestedStringForKey).toHaveBeenCalledOnceWith(
      LocalStorageKey.RecentPrompts,
      PromptType.SendChatMessage,
      'prompt-a',
    );

    const request = generateTextService.generateText.calls.mostRecent()
      .args[0] as GenerateTextRequestDto;
    const expectedContext: SendChatMessageContextInfoDto = {
      $type: NovelTextGenerationType.SendChatMessage,
      novelId: 'novel-1',
      chapterIndex: 2,
      userMessage: 'New question',
      previousMessages: [
        { role: ChatMessageRole.User, textContent: 'Earlier question' },
        {
          role: ChatMessageRole.Assistant,
          textContent: 'Earlier answer',
        },
      ],
      compendiumIds: ['compendium-1'],
      compendiumRecordIds: ['record-1'],
    };
    const expectedRequest: GenerateTextRequestDto = {
      model: 'model-a',
      promptId: 'prompt-a',
      contextInfo: expectedContext,
    };
    expect(request).toEqual(expectedRequest);
    expect(component.isGenerating).toBeTrue();
  });

  it('applies streamed snapshots and saves the completed conversation once', () => {
    component.userInput = 'Question';
    selectGenerationOptions();
    component.sendMessage();
    const assistantMessage = component.currentChat.messages[1];

    generation.next({ content: 'Partial', isComplete: false });
    expect(assistantMessage.textContent).toBe('Partial');
    expect(component.isGenerating).toBeTrue();
    expect(chatService.updateChat).not.toHaveBeenCalled();

    generation.next({ content: '', isComplete: false });
    expect(assistantMessage.textContent).toBe('Partial');

    generation.next({ content: 'Complete answer', isComplete: true });
    generation.next({ content: 'Late answer', isComplete: true });

    expect(assistantMessage.textContent).toBe('Complete answer');
    expect(component.isGenerating).toBeFalse();
    expect(chatService.updateChat).toHaveBeenCalledTimes(1);
  });

  it('finishes and saves when a stream completes without a completion update', () => {
    generateTextService.generateText.and.returnValue(
      of({ content: 'Answer', isComplete: false }),
    );
    component.userInput = 'Question';
    selectGenerationOptions();

    component.sendMessage();

    expect(component.currentChat.messages[1].textContent).toBe('Answer');
    expect(component.isGenerating).toBeFalse();
    expect(chatService.updateChat).toHaveBeenCalledTimes(1);
  });

  it('removes an empty failed response and persists the user message', () => {
    const error = new Error('generation failed');
    const consoleError = spyOn(console, 'error');
    component.userInput = 'Question';
    selectGenerationOptions();
    component.sendMessage();

    generation.error(error);

    expect(consoleError).toHaveBeenCalledOnceWith(
      'Error generating text:',
      error,
    );
    expect(component.isGenerating).toBeFalse();
    expect(component.currentChat.messages.length).toBe(1);
    expect(component.currentChat.messages[0].role).toBe(ChatMessageRole.User);
    expect(chatService.updateChat).toHaveBeenCalledTimes(1);
  });

  it('retains and persists a partial response when generation fails', () => {
    spyOn(console, 'error');
    component.userInput = 'Question';
    selectGenerationOptions();
    component.sendMessage();
    generation.next({ content: 'Partial answer', isComplete: false });

    generation.error(new Error('generation failed'));

    expect(component.currentChat.messages[1].textContent).toBe(
      'Partial answer',
    );
    expect(chatService.updateChat).toHaveBeenCalledTimes(1);
  });

  it('resends an unmatched user message directly after that message', () => {
    const userMessage = message(
      'user-message',
      ChatMessageRole.User,
      'Try again',
    );
    const laterUserMessage = message(
      'later-user-message',
      ChatMessageRole.User,
      'Later',
    );
    component.currentChat.messages = [userMessage, laterUserMessage];
    selectGenerationOptions();

    component.resendMessage(userMessage);

    expect(component.currentChat.messages.length).toBe(3);
    expect(component.currentChat.messages[1].role).toBe(
      ChatMessageRole.Assistant,
    );
    expect(component.currentChat.messages[2]).toBe(laterUserMessage);
    expect(generateTextService.generateText).toHaveBeenCalledTimes(1);
  });

  it('does not insert a resend placeholder without generation options', () => {
    const userMessage = message(
      'user-message',
      ChatMessageRole.User,
      'Try again',
    );
    component.currentChat.messages = [userMessage];

    component.resendMessage(userMessage);

    expect(component.currentChat.messages).toEqual([userMessage]);
    expect(generateTextService.generateText).not.toHaveBeenCalled();
  });

  it('opens message editing and saves a changed result', () => {
    const editedMessage = message(
      'message-1',
      ChatMessageRole.User,
      'Original',
    );

    component.editMessage(editedMessage);
    expect(dialogService.open).toHaveBeenCalledOnceWith(
      EditChatMessageComponent,
      jasmine.objectContaining({
        header: 'Edit Message',
        width: '50vw',
        data: { text: 'Original' },
      }),
    );

    dialogClose.next('Edited');
    expect(editedMessage.textContent).toBe('Edited');
    expect(chatService.updateChat).toHaveBeenCalledTimes(1);
  });

  it('does not save an unchanged or dismissed edit', () => {
    const editedMessage = message(
      'message-1',
      ChatMessageRole.User,
      'Original',
    );
    component.editMessage(editedMessage);

    dialogClose.next('Original');
    dialogClose.next(undefined);

    expect(chatService.updateChat).not.toHaveBeenCalled();
  });

  it('deletes and persists a message only after confirmation', () => {
    component.currentChat.messages = [
      message('keep', ChatMessageRole.User, 'Keep'),
      message('delete', ChatMessageRole.Assistant, 'Delete'),
    ];

    component.deleteMessage('delete');
    expect(component.currentChat.messages.length).toBe(2);

    const confirmation = confirmationService.confirm.calls.mostRecent()
      .args[0];
    confirmation.accept?.();

    expect(component.currentChat.messages.map((item) => item.id)).toEqual([
      'keep',
    ]);
    expect(chatService.updateChat).toHaveBeenCalledTimes(1);
  });

  it('copies a message and reports success', fakeAsync(() => {
    const writeText = jasmine
      .createSpy('writeText')
      .and.returnValue(Promise.resolve());
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText },
    });

    component.copyMessage('Copy me');
    flushMicrotasks();

    expect(writeText).toHaveBeenCalledOnceWith('Copy me');
    expect(toastr.success).toHaveBeenCalledOnceWith(
      'Message copied to clipboard',
    );
  }));

  it('reports clipboard write failures', fakeAsync(() => {
    const writeText = jasmine
      .createSpy('writeText')
      .and.returnValue(Promise.reject(new Error('not allowed')));
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText },
    });

    component.copyMessage('Copy me');
    flushMicrotasks();

    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Could not copy message to clipboard',
    );
  }));

  it('cancels generation and closes the edit dialog when destroyed', () => {
    component.userInput = 'Question';
    selectGenerationOptions();
    component.sendMessage();
    component.editMessage(
      message('message-1', ChatMessageRole.User, 'Original'),
    );

    component.ngOnDestroy();
    generation.next({ content: 'Late answer', isComplete: true });

    expect(component.isGenerating).toBeFalse();
    expect(component.currentChat.messages[1].textContent).toBe('');
    expect(chatService.updateChat).not.toHaveBeenCalled();
    expect(dialogRef.close).toHaveBeenCalledTimes(1);
  });
});
