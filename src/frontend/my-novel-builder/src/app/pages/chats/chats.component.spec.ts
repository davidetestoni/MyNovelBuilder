import { TestBed } from '@angular/core/testing';
import {
  ActivatedRoute,
  convertToParamMap,
  ParamMap,
  Router,
} from '@angular/router';
import { ConfirmationService } from 'primeng/api';
import type { Confirmation } from 'primeng/api';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { of, Subject } from 'rxjs';
import { ChatService } from '../../services/chat.service';
import { NovelService } from '../../services/novel.service';
import type { Chat } from '../../types/dtos/chats/chat';
import type { ChatMetadata } from '../../types/dtos/chats/chat-metadata';
import type { NovelDto } from '../../types/dtos/novel/novel.dto';
import { ChatsComponent } from './chats.component';

describe('ChatsComponent workflows', () => {
  let component: ChatsComponent;
  let chatService: jasmine.SpyObj<ChatService>;
  let novelService: jasmine.SpyObj<NovelService>;
  let confirmationService: jasmine.SpyObj<ConfirmationService>;
  let dialogService: jasmine.SpyObj<DialogService>;
  let router: jasmine.SpyObj<Router>;
  let routeParams: Subject<ParamMap>;
  let route: {
    paramMap: ReturnType<Subject<ParamMap>['asObservable']>;
    snapshot: { paramMap: ParamMap };
  };

  const metadata = (
    id: string,
    novelId = 'novel-id',
    name: string | null = `Chat ${id}`,
  ): ChatMetadata => ({
    id,
    novelId,
    name,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-02T00:00:00Z',
  });

  const chat = (
    id: string,
    novelId = 'novel-id',
    name: string | null = `Chat ${id}`,
  ): Chat => ({
    id,
    name,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-02T00:00:00Z',
    context: {
      novelId,
      chapterIndex: null,
      compendiumIds: [],
      compendiumRecordIds: [],
    },
    messages: [],
  });

  const emitRoute = (id?: string): void => {
    route.snapshot.paramMap = convertToParamMap(id ? { id } : {});
    routeParams.next(route.snapshot.paramMap);
  };

  beforeEach(() => {
    chatService = jasmine.createSpyObj<ChatService>('ChatService', [
      'getChats',
      'getChat',
      'deleteChat',
    ]);
    novelService = jasmine.createSpyObj<NovelService>('NovelService', [
      'getNovels',
    ]);
    confirmationService = jasmine.createSpyObj<ConfirmationService>(
      'ConfirmationService',
      ['confirm'],
    );
    dialogService = jasmine.createSpyObj<DialogService>('DialogService', [
      'open',
    ]);
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    routeParams = new Subject<ParamMap>();
    route = {
      paramMap: routeParams.asObservable(),
      snapshot: { paramMap: convertToParamMap({}) },
    };

    chatService.getChats.and.returnValue(of([]));
    chatService.getChat.and.callFake((id) => of(chat(id)));
    chatService.deleteChat.and.returnValue(of(undefined));
    novelService.getNovels.and.returnValue(of([]));
    router.navigate.and.resolveTo(true);

    TestBed.configureTestingModule({
      providers: [
        { provide: ChatService, useValue: chatService },
        { provide: NovelService, useValue: novelService },
        { provide: ConfirmationService, useValue: confirmationService },
        { provide: DialogService, useValue: dialogService },
        { provide: Router, useValue: router },
        { provide: ActivatedRoute, useValue: route },
      ],
    });

    component = TestBed.runInInjectionContext(() => new ChatsComponent());
  });

  it('loads the routed chat, chat list, and novel covers', () => {
    const chatsResponse = new Subject<ChatMetadata[]>();
    const novelsResponse = new Subject<NovelDto[]>();
    const chatResponse = new Subject<Chat>();
    const selectedChat = chat('selected');
    chatService.getChats.and.returnValue(chatsResponse);
    chatService.getChat.and.returnValue(chatResponse);
    novelService.getNovels.and.returnValue(novelsResponse);

    component.ngOnInit();
    emitRoute('selected');

    expect(chatService.getChats).toHaveBeenCalledTimes(1);
    expect(novelService.getNovels).toHaveBeenCalledTimes(1);
    expect(chatService.getChat).toHaveBeenCalledOnceWith('selected');

    chatsResponse.next([metadata('selected')]);
    chatResponse.next(selectedChat);
    novelsResponse.next([
      {
        id: 'novel-id',
        coverImageUrl: '/covers/novel.webp',
      } as NovelDto,
    ]);

    expect(component.chats).toEqual([metadata('selected')]);
    expect(component.currentChat).toBe(selectedChat);
    expect(component.currentChatId).toBe('selected');
    expect(component.getChatNovelCover(metadata('selected'))).toBe(
      '/covers/novel.webp',
    );
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('navigates to the first chat when no route selection exists', () => {
    chatService.getChats.and.returnValue(
      of([metadata('first'), metadata('second')]),
    );

    component.ngOnInit();

    expect(router.navigate).toHaveBeenCalledOnceWith(['/chat', 'first']);
  });

  it('does not reload the currently selected chat', () => {
    const selectedChat = chat('selected');
    chatService.getChat.and.returnValue(of(selectedChat));

    component.loadChat('selected');
    component.loadChat('selected');

    expect(chatService.getChat).toHaveBeenCalledOnceWith('selected');
    expect(component.currentChat).toBe(selectedChat);
  });

  it('synchronizes changed detail metadata into the chat list', () => {
    component.chats = [metadata('selected', 'novel-id', 'Old name')];
    component.currentChatId = 'selected';
    component.currentChat = chat('selected', 'novel-id', 'New name');
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date('2026-07-28T12:00:00Z'));

    try {
      component.updateLocalChatMetadata();
    } finally {
      jasmine.clock().uninstall();
    }

    expect(component.chats[0].name).toBe('New name');
    expect(component.chats[0].updatedAt).toBe('2026-07-28T12:00:00.000Z');
  });

  it('deletes the current chat only after confirmation and returns to the list', () => {
    component.chats = [metadata('selected'), metadata('remaining')];
    component.currentChatId = 'selected';

    component.deleteChat('selected');

    expect(chatService.deleteChat).not.toHaveBeenCalled();

    const confirmation = confirmationService.confirm.calls.mostRecent()
      .args[0] as Confirmation;
    confirmation.accept?.();

    expect(chatService.deleteChat).toHaveBeenCalledOnceWith('selected');
    expect(component.chats).toEqual([metadata('remaining')]);
    expect(router.navigate).toHaveBeenCalledOnceWith(['/chat']);
  });

  it('prepends a chat returned by the create dialog and navigates to it', () => {
    const createdChat = chat('created', 'other-novel', 'Created chat');
    const onClose = new Subject<Chat | undefined>();
    const dialogRef = {
      onClose,
      close: jasmine.createSpy('close'),
    } as unknown as DynamicDialogRef;
    dialogService.open.and.returnValue(dialogRef);
    component.chats = [metadata('existing')];

    component.openCreateChatDialog();
    onClose.next(createdChat);

    expect(component.chats).toEqual([
      {
        id: 'created',
        novelId: 'other-novel',
        name: 'Created chat',
        createdAt: createdChat.createdAt,
        updatedAt: createdChat.updatedAt,
      },
      metadata('existing'),
    ]);
    expect(router.navigate).toHaveBeenCalledOnceWith(['/chat', 'created']);
  });

  it('closes an open create dialog when the page is destroyed', () => {
    const dialogRef = {
      onClose: new Subject<Chat | undefined>(),
      close: jasmine.createSpy('close'),
    } as unknown as DynamicDialogRef;
    dialogService.open.and.returnValue(dialogRef);

    component.openCreateChatDialog();
    component.ngOnDestroy();

    expect(dialogRef.close).toHaveBeenCalledTimes(1);
  });
});
