import { TestBed } from '@angular/core/testing';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { of, Subject } from 'rxjs';
import { CreateNovelComponent } from '../../components/create-novel/create-novel.component';
import { NovelService } from '../../services/novel.service';
import type { NovelDto } from '../../types/dtos/novel/novel.dto';
import { WritingLanguage } from '../../types/enums/writing-language';
import { WritingPov } from '../../types/enums/writing-pov';
import { WritingTense } from '../../types/enums/writing-tense';
import { NovelsComponent } from './novels.component';

describe('NovelsComponent workflows', () => {
  let component: NovelsComponent;
  let novelService: jasmine.SpyObj<NovelService>;
  let dialogService: jasmine.SpyObj<DialogService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let dialogClosed: Subject<boolean | undefined>;

  const novel = (
    id: string,
    overrides: Partial<NovelDto> = {},
  ): NovelDto => ({
    id,
    createdAt: '2026-07-01T12:00:00Z',
    updatedAt: '2026-07-27T12:00:00Z',
    title: `Novel ${id}`,
    author: 'Author',
    brief: 'A brief',
    coverImageUrl: null,
    tense: WritingTense.Present,
    pov: WritingPov.FirstPerson,
    language: WritingLanguage.English,
    rpgMode: false,
    mainCharacterId: null,
    compendiumIds: [],
    ...overrides,
  });

  beforeEach(() => {
    novelService = jasmine.createSpyObj<NovelService>('NovelService', [
      'getNovels',
    ]);
    dialogService = jasmine.createSpyObj<DialogService>('DialogService', [
      'open',
    ]);
    dialogClosed = new Subject<boolean | undefined>();
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>(
      'DynamicDialogRef',
      ['close'],
      { onClose: dialogClosed.asObservable() },
    );

    novelService.getNovels.and.returnValue(of([]));
    dialogService.open.and.returnValue(dialogRef);

    TestBed.configureTestingModule({
      providers: [
        { provide: NovelService, useValue: novelService },
        { provide: DialogService, useValue: dialogService },
      ],
    });

    component = TestBed.runInInjectionContext(() => new NovelsComponent());
  });

  it('starts in the loading state and loads novels on initialization', () => {
    const novels = [novel('one'), novel('two')];
    novelService.getNovels.and.returnValue(of(novels));

    expect(component.novels).toBeNull();

    component.ngOnInit();

    expect(novelService.getNovels).toHaveBeenCalledTimes(1);
    expect(component.novels).toBe(novels);
  });

  it('keeps the loading state until the novel request responds', () => {
    const response = new Subject<NovelDto[]>();
    novelService.getNovels.and.returnValue(response);

    component.getNovels();
    expect(component.novels).toBeNull();

    const novels = [novel('later')];
    response.next(novels);

    expect(component.novels).toBe(novels);
  });

  it('replaces the current list when novels are reloaded', () => {
    component.novels = [novel('old')];
    novelService.getNovels.and.returnValue(of([novel('new')]));

    component.getNovels();

    expect(component.novels).toEqual([novel('new')]);
  });

  it('formats the last update relative to the current time', () => {
    jasmine.clock().install();
    try {
      jasmine.clock().mockDate(new Date('2026-07-29T12:00:00Z'));

      expect(component.getLastUpdated(novel('one'))).toBe('2 days ago');
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('derives stable translucent cover colors from novel ids', () => {
    const firstColor = component.guidToColor('novel-one');

    expect(firstColor).toMatch(/^#[0-9a-f]{8}$/);
    expect(component.guidToColor('novel-one')).toBe(firstColor);
    expect(component.guidToColor('novel-two')).not.toBe(firstColor);
    expect(firstColor.endsWith('50')).toBeTrue();
  });

  it('uses the transparent black fallback for an empty id', () => {
    expect(component.guidToColor('')).toBe('#00000050');
  });

  it('opens the create-novel dialog with the expected modal settings', () => {
    component.openCreateNovelDialog();

    expect(dialogService.open).toHaveBeenCalledOnceWith(CreateNovelComponent, {
      header: 'Create a novel',
      width: '50vw',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      modal: true,
      closable: true,
      closeOnEscape: true,
      dismissableMask: true,
    });
  });

  it('reloads novels only when creation succeeds', () => {
    component.openCreateNovelDialog();
    novelService.getNovels.calls.reset();

    dialogClosed.next(undefined);
    dialogClosed.next(false);
    expect(novelService.getNovels).not.toHaveBeenCalled();

    dialogClosed.next(true);
    expect(novelService.getNovels).toHaveBeenCalledTimes(1);
  });

  it('closes an open create dialog when the page is destroyed', () => {
    component.openCreateNovelDialog();

    component.ngOnDestroy();

    expect(dialogRef.close).toHaveBeenCalledTimes(1);
  });

  it('can be destroyed without first opening a dialog', () => {
    expect(() => component.ngOnDestroy()).not.toThrow();
    expect(dialogRef.close).not.toHaveBeenCalled();
  });
});
