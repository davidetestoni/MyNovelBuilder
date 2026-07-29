import { TestBed } from '@angular/core/testing';
import { DynamicDialogRef } from 'primeng/dynamicdialog';
import { ToastrService } from 'ngx-toastr';
import { of, Subject, throwError } from 'rxjs';
import { ChatService } from '../../services/chat.service';
import { NovelService } from '../../services/novel.service';
import { Chat } from '../../types/dtos/chats/chat';
import { NovelDto } from '../../types/dtos/novel/novel.dto';
import { WritingLanguage } from '../../types/enums/writing-language';
import { WritingPov } from '../../types/enums/writing-pov';
import { WritingTense } from '../../types/enums/writing-tense';
import { CreateChatComponent } from './create-chat.component';

describe('CreateChatComponent workflow', () => {
  let component: CreateChatComponent;
  let novelService: jasmine.SpyObj<NovelService>;
  let chatService: jasmine.SpyObj<ChatService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let toastr: jasmine.SpyObj<ToastrService>;

  const novel = (): NovelDto => ({
    id: 'novel-1',
    createdAt: '2026-07-29T12:00:00Z',
    updatedAt: '2026-07-29T12:00:00Z',
    title: 'Novel',
    author: '',
    brief: '',
    coverImageUrl: null,
    tense: WritingTense.Present,
    pov: WritingPov.FirstPerson,
    language: WritingLanguage.English,
    rpgMode: false,
    mainCharacterId: null,
    compendiumIds: [],
  });

  const createdChat = (): Chat => ({
    id: 'chat-1',
    createdAt: '2026-07-29T12:00:00Z',
    updatedAt: '2026-07-29T12:00:00Z',
    name: null,
    context: {
      novelId: 'novel-1',
      chapterIndex: null,
      compendiumIds: [],
      compendiumRecordIds: [],
    },
    messages: [],
  });

  beforeEach(() => {
    novelService = jasmine.createSpyObj<NovelService>('NovelService', [
      'getNovels',
    ]);
    chatService = jasmine.createSpyObj<ChatService>('ChatService', [
      'createChat',
    ]);
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>('DynamicDialogRef', [
      'close',
    ]);
    toastr = jasmine.createSpyObj<ToastrService>('ToastrService', [
      'success',
      'error',
    ]);
    novelService.getNovels.and.returnValue(of([novel()]));
    chatService.createChat.and.returnValue(of(createdChat()));

    TestBed.configureTestingModule({
      providers: [
        { provide: NovelService, useValue: novelService },
        { provide: ChatService, useValue: chatService },
        { provide: DynamicDialogRef, useValue: dialogRef },
        { provide: ToastrService, useValue: toastr },
      ],
    });

    component = TestBed.runInInjectionContext(
      () => new CreateChatComponent(),
    );
  });

  it('starts in a loading state with a required novel selection', () => {
    expect(component.novels).toBeNull();
    expect(component.formGroup.invalid).toBeTrue();
    expect(component.formGroup.controls.novel.hasError('required')).toBeTrue();
    expect(component.isCreating).toBeFalse();
  });

  it('loads the available novels on initialization', () => {
    component.ngOnInit();

    expect(novelService.getNovels).toHaveBeenCalledTimes(1);
    expect(component.novels).toEqual([novel()]);
  });

  it('ends loading and reports when novels cannot be loaded', () => {
    novelService.getNovels.and.returnValue(
      throwError(() => new Error('request failed')),
    );

    component.ngOnInit();

    expect(component.novels).toEqual([]);
    expect(toastr.error).toHaveBeenCalledOnceWith('Could not load novels.');
  });

  it('does not submit without a selected novel', () => {
    component.createChat();

    expect(chatService.createChat).not.toHaveBeenCalled();
    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('creates a chat for the selected novel and closes with the result', () => {
    component.formGroup.controls.novel.setValue(novel());

    component.createChat();

    expect(chatService.createChat).toHaveBeenCalledOnceWith({
      novelId: 'novel-1',
    });
    expect(toastr.success).toHaveBeenCalledOnceWith(
      'Chat created successfully.',
    );
    expect(dialogRef.close).toHaveBeenCalledOnceWith(createdChat());
    expect(component.isCreating).toBeFalse();
  });

  it('prevents duplicate creation while a request is pending', () => {
    const response = new Subject<Chat>();
    chatService.createChat.and.returnValue(response);
    component.formGroup.controls.novel.setValue(novel());

    component.createChat();
    component.createChat();

    expect(component.isCreating).toBeTrue();
    expect(chatService.createChat).toHaveBeenCalledTimes(1);

    response.next(createdChat());
    response.complete();
    expect(component.isCreating).toBeFalse();
  });

  it('restores the form and reports a creation error', () => {
    chatService.createChat.and.returnValue(
      throwError(() => new Error('request failed')),
    );
    component.formGroup.controls.novel.setValue(novel());

    component.createChat();

    expect(component.isCreating).toBeFalse();
    expect(toastr.error).toHaveBeenCalledOnceWith('Could not create chat.');
    expect(toastr.success).not.toHaveBeenCalled();
    expect(dialogRef.close).not.toHaveBeenCalled();
  });
});
