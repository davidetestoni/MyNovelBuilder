import { HttpHeaders, HttpResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { ConfirmationService } from 'primeng/api';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { of, Subject } from 'rxjs';
import {
  TranslateNovelDialogComponent,
  type TranslateNovelDialogResult,
} from '../../components/translate-novel-dialog/translate-novel-dialog.component';
import { CompendiumService } from '../../services/compendium.service';
import { NovelService } from '../../services/novel.service';
import { PromptService } from '../../services/prompt.service';
import type { CompendiumRecordOverviewDto } from '../../types/dtos/compendium-record/compendium-record-overview.dto';
import type { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import type { NovelDto } from '../../types/dtos/novel/novel.dto';
import type { Prose } from '../../types/dtos/novel/prose';
import type { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { PromptType } from '../../types/enums/prompt-type';
import { WritingLanguage } from '../../types/enums/writing-language';
import { WritingPov } from '../../types/enums/writing-pov';
import { WritingTense } from '../../types/enums/writing-tense';
import { NovelSettingsComponent } from './novel-settings.component';

describe('NovelSettingsComponent workflows', () => {
  let component: NovelSettingsComponent;
  let novelService: jasmine.SpyObj<NovelService>;
  let compendiumService: jasmine.SpyObj<CompendiumService>;
  let promptService: jasmine.SpyObj<PromptService>;
  let router: jasmine.SpyObj<Router>;
  let dialogService: jasmine.SpyObj<DialogService>;
  let confirmationService: jasmine.SpyObj<ConfirmationService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let dialogClosed: Subject<TranslateNovelDialogResult | undefined>;

  const novel = (): NovelDto => ({
    id: 'novel-id',
    createdAt: '2025-01-01T00:00:00Z',
    updatedAt: '2025-02-01T00:00:00Z',
    title: 'Novel title',
    author: 'Author',
    brief: 'Brief',
    coverImageUrl: null,
    tense: WritingTense.Past,
    pov: WritingPov.ThirdPersonLimited,
    language: WritingLanguage.English,
    rpgMode: false,
    mainCharacterId: null,
    compendiumIds: ['selected-compendium'],
  });

  const record = (
    id: string,
    name: string,
    type = CompendiumRecordType.Character,
  ): CompendiumRecordOverviewDto => ({
    id,
    name,
    type,
    imageUrl: null,
  });

  const compendium = (
    id: string,
    name: string,
    createdAt: string,
    updatedAt: string,
    records: CompendiumRecordOverviewDto[] = [],
  ): CompendiumDto => ({
    id,
    name,
    description: `${name} description`,
    createdAt,
    updatedAt,
    records,
  });

  const prose = (): Prose => ({
    chapters: [
      {
        title: 'Chapter 1',
        sections: [],
        storyEvents: [],
      },
    ],
  });

  const prompt = (id: string, type: PromptType): PromptDto => ({
    id,
    createdAt: '2025-01-01T00:00:00Z',
    updatedAt: '2025-01-01T00:00:00Z',
    name: id,
    type,
    messages: [],
  });

  beforeEach(() => {
    novelService = jasmine.createSpyObj<NovelService>('NovelService', [
      'getNovel',
      'updateNovel',
      'uploadNovelCoverImage',
      'exportNovel',
      'getNovelProse',
      'deleteNovel',
    ]);
    compendiumService = jasmine.createSpyObj<CompendiumService>(
      'CompendiumService',
      ['getCompendia'],
    );
    promptService = jasmine.createSpyObj<PromptService>('PromptService', [
      'getPrompts',
    ]);
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    dialogService = jasmine.createSpyObj<DialogService>('DialogService', [
      'open',
    ]);
    confirmationService = jasmine.createSpyObj<ConfirmationService>(
      'ConfirmationService',
      ['confirm'],
    );
    const toastrService = jasmine.createSpyObj<ToastrService>(
      'ToastrService',
      ['error'],
    );
    dialogClosed = new Subject<TranslateNovelDialogResult | undefined>();
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>(
      'DynamicDialogRef',
      ['close'],
      { onClose: dialogClosed.asObservable() },
    );

    novelService.getNovel.and.returnValue(of(novel()));
    novelService.updateNovel.and.returnValue(of(novel()));
    novelService.uploadNovelCoverImage.and.returnValue(of(undefined));
    novelService.getNovelProse.and.returnValue(of(prose()));
    novelService.deleteNovel.and.returnValue(of(undefined));
    compendiumService.getCompendia.and.returnValue(of([]));
    promptService.getPrompts.and.returnValue(of([]));
    router.navigate.and.resolveTo(true);
    dialogService.open.and.returnValue(dialogRef);

    TestBed.configureTestingModule({
      providers: [
        { provide: NovelService, useValue: novelService },
        { provide: CompendiumService, useValue: compendiumService },
        { provide: PromptService, useValue: promptService },
        { provide: Router, useValue: router },
        { provide: DialogService, useValue: dialogService },
        { provide: ConfirmationService, useValue: confirmationService },
        { provide: ToastrService, useValue: toastrService },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ id: 'novel-id' }),
            },
          },
        },
      ],
    });

    component = TestBed.runInInjectionContext(
      () => new NovelSettingsComponent(),
    );
  });

  it('loads the route novel and sorts compendia by their latest timestamp', () => {
    const loadedNovel = novel();
    const older = compendium(
      'older',
      'Older',
      '2024-01-01T00:00:00Z',
      '',
    );
    const invalid = compendium('invalid', 'Invalid', 'not-a-date', '');
    const newer = compendium(
      'newer',
      'Newer',
      '2024-01-01T00:00:00Z',
      '2025-01-01T00:00:00Z',
    );
    novelService.getNovel.and.returnValue(of(loadedNovel));
    compendiumService.getCompendia.and.returnValue(
      of([older, invalid, newer]),
    );

    component.ngOnInit();

    expect(component.novelId).toBe('novel-id');
    expect(component.novel).toBe(loadedNovel);
    expect(component.compendia).toEqual([newer, older, invalid]);
    expect(novelService.getNovel).toHaveBeenCalledOnceWith('novel-id');
    expect(compendiumService.getCompendia).toHaveBeenCalledTimes(1);
  });

  it('navigates back to the prose editor', async () => {
    component.ngOnInit();

    await component.goToProse();

    expect(router.navigate).toHaveBeenCalledOnceWith([
      '/novel',
      'novel-id',
    ]);
  });

  it('persists only the editable novel settings on blur', () => {
    const loadedNovel = novel();
    loadedNovel.title = 'Updated title';
    loadedNovel.rpgMode = true;
    component.novel = loadedNovel;

    component.onBlur();

    expect(novelService.updateNovel).toHaveBeenCalledOnceWith({
      id: 'novel-id',
      title: 'Updated title',
      author: 'Author',
      brief: 'Brief',
      tense: WritingTense.Past,
      pov: WritingPov.ThirdPersonLimited,
      language: WritingLanguage.English,
      rpgMode: true,
      mainCharacterId: null,
      compendiumIds: ['selected-compendium'],
    });
  });

  it('adds and removes compendia while persisting each change', () => {
    component.novel = novel();

    component.toggleCompendium('new-compendium');

    expect(component.novel.compendiumIds).toEqual([
      'selected-compendium',
      'new-compendium',
    ]);
    expect(novelService.updateNovel).toHaveBeenCalledTimes(1);

    component.toggleCompendium('selected-compendium');

    expect(component.novel.compendiumIds).toEqual(['new-compendium']);
    expect(novelService.updateNovel).toHaveBeenCalledTimes(2);
  });

  it('returns alphabetized characters from selected compendia only', () => {
    component.novel = novel();
    component.compendia = [
      compendium('selected-compendium', 'Selected', '', '', [
        record('zara', 'Zara'),
        record('place', 'Castle', CompendiumRecordType.Place),
        record('anna', 'Anna'),
      ]),
      compendium('unselected', 'Unselected', '', '', [
        record('ignored', 'Ignored'),
      ]),
    ];

    expect(component.getAvailableCharacters().map(({ id }) => id)).toEqual([
      'anna',
      'zara',
    ]);

    component.compendia = null;
    expect(component.getAvailableCharacters()).toEqual([]);
  });

  it('uploads a selected cover image and refreshes the novel', () => {
    component.ngOnInit();
    novelService.getNovel.calls.reset();
    const file = new File(['cover'], 'cover.png', { type: 'image/png' });

    component.updateNovelCoverImage({
      target: { files: [file] },
    } as unknown as Event);

    expect(novelService.uploadNovelCoverImage).toHaveBeenCalledOnceWith(
      'novel-id',
      file,
    );
    expect(novelService.getNovel).toHaveBeenCalledOnceWith('novel-id');
  });

  it('ignores a cover-image event without a selected file', () => {
    component.novel = novel();

    component.updateNovelCoverImage({
      target: { files: [] },
    } as unknown as Event);

    expect(novelService.uploadNovelCoverImage).not.toHaveBeenCalled();
  });

  it('exports using the response filename and releases the object URL', async () => {
    component.novel = novel();
    const blob = new Blob(['novel'], { type: 'text/html' });
    novelService.exportNovel.and.returnValue(
      of(
        new HttpResponse({
          body: blob,
          headers: new HttpHeaders({
            'Content-Disposition': 'attachment; filename="story.html"',
          }),
        }),
      ),
    );
    spyOn(window.URL, 'createObjectURL').and.returnValue('blob:novel');
    spyOn(window.URL, 'revokeObjectURL');
    let downloadedFileName = '';
    spyOn(HTMLAnchorElement.prototype, 'click').and.callFake(function (
      this: HTMLAnchorElement,
    ) {
      downloadedFileName = this.download;
    });

    await component.exportNovel('html');

    expect(novelService.exportNovel).toHaveBeenCalledOnceWith(
      'novel-id',
      'html',
    );
    expect(downloadedFileName).toBe('story.html');
    expect(window.URL.createObjectURL).toHaveBeenCalledOnceWith(blob);
    expect(window.URL.revokeObjectURL).toHaveBeenCalledOnceWith('blob:novel');
  });

  it('uses the expected fallback extension when export headers omit a filename', async () => {
    component.novel = novel();
    novelService.exportNovel.and.returnValue(
      of(new HttpResponse({ body: new Blob(['novel']) })),
    );
    spyOn(window.URL, 'createObjectURL').and.returnValue('blob:novel');
    spyOn(window.URL, 'revokeObjectURL');
    let downloadedFileName = '';
    spyOn(HTMLAnchorElement.prototype, 'click').and.callFake(function (
      this: HTMLAnchorElement,
    ) {
      downloadedFileName = this.download;
    });

    await component.exportNovel('markdown');

    expect(downloadedFileName).toBe('novel-id.md');
  });

  it('does not create a download when an export has no response body', async () => {
    component.novel = novel();
    novelService.exportNovel.and.returnValue(
      of(new HttpResponse<Blob>({ body: null })),
    );
    spyOn(window.URL, 'createObjectURL');

    await component.exportNovel('pdf');

    expect(window.URL.createObjectURL).not.toHaveBeenCalled();
  });

  it('opens translation with matching prompts and navigates on completion', async () => {
    const loadedNovel = novel();
    const loadedProse = prose();
    const translatePrompt = prompt('translate', PromptType.TranslateNovel);
    const unrelatedPrompt = prompt('generate', PromptType.GenerateText);
    component.novel = loadedNovel;
    novelService.getNovelProse.and.returnValue(of(loadedProse));
    promptService.getPrompts.and.returnValue(
      of([unrelatedPrompt, translatePrompt]),
    );

    await component.openTranslateDialog();

    expect(novelService.getNovelProse).toHaveBeenCalledOnceWith('novel-id');
    expect(promptService.getPrompts).toHaveBeenCalledTimes(1);
    expect(dialogService.open).toHaveBeenCalledOnceWith(
      TranslateNovelDialogComponent,
      jasmine.objectContaining({
        header: 'Translate novel',
        modal: true,
        data: {
          novel: loadedNovel,
          prose: loadedProse,
          prompts: [translatePrompt],
        },
      }),
    );

    dialogClosed.next(undefined);
    expect(router.navigate).not.toHaveBeenCalled();

    dialogClosed.next({ novelId: 'translated-novel' });
    expect(router.navigate).toHaveBeenCalledOnceWith([
      '/novel',
      'translated-novel',
    ]);
  });

  it('closes an open translation dialog when the component is destroyed', async () => {
    component.novel = novel();
    await component.openTranslateDialog();

    component.ngOnDestroy();

    expect(dialogRef.close).toHaveBeenCalledTimes(1);
  });

  it('deletes the novel only after confirmation and returns to the novel list', () => {
    component.novel = novel();

    component.confirmDeleteNovel();

    expect(confirmationService.confirm).toHaveBeenCalledOnceWith(
      jasmine.objectContaining({
        message:
          'Are you sure you want to delete this novel? This action cannot be undone.',
        header: 'Confirm Delete',
        icon: 'pi pi-exclamation-triangle',
        acceptButtonStyleClass: 'p-button-danger',
        accept: jasmine.any(Function),
      }),
    );
    expect(novelService.deleteNovel).not.toHaveBeenCalled();

    confirmationService.confirm.calls.mostRecent().args[0].accept!();

    expect(novelService.deleteNovel).toHaveBeenCalledOnceWith('novel-id');
    expect(router.navigate).toHaveBeenCalledOnceWith(['/novels']);
  });

  it('guards novel-specific actions until the novel has loaded', async () => {
    component.novel = null;

    component.onBlur();
    component.toggleCompendium('compendium');
    component.updateNovelCoverImage({
      target: { files: [new File(['cover'], 'cover.png')] },
    } as unknown as Event);
    await component.exportNovel('pdf');
    await component.openTranslateDialog();
    component.confirmDeleteNovel();

    expect(novelService.updateNovel).not.toHaveBeenCalled();
    expect(novelService.uploadNovelCoverImage).not.toHaveBeenCalled();
    expect(novelService.exportNovel).not.toHaveBeenCalled();
    expect(dialogService.open).not.toHaveBeenCalled();
    expect(confirmationService.confirm).not.toHaveBeenCalled();
  });
});
