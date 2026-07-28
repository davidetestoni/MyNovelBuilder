import { TestBed } from '@angular/core/testing';
import {
  ActivatedRoute,
  convertToParamMap,
  Router,
} from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { of } from 'rxjs';
import { CompendiumService } from '../../services/compendium.service';
import { NovelService } from '../../services/novel.service';
import { PromptService } from '../../services/prompt.service';
import type { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import type { CompendiumRecordMediaDto } from '../../types/dtos/compendium-record/compendium-record-media.dto';
import type { NovelDto } from '../../types/dtos/novel/novel.dto';
import type { Prose } from '../../types/dtos/novel/prose';
import { NovelEditorComponent } from './novel-editor.component';

describe('NovelEditorComponent workflows', () => {
  let component: NovelEditorComponent;
  let novelService: jasmine.SpyObj<NovelService>;
  let promptService: jasmine.SpyObj<PromptService>;
  let compendiumService: jasmine.SpyObj<CompendiumService>;
  let toastrService: jasmine.SpyObj<ToastrService>;
  let router: jasmine.SpyObj<Router>;
  let queryParams: Record<string, string>;

  const createProse = (): Prose => ({
    chapters: [
      {
        title: 'Chapter 1',
        sections: [],
        storyEvents: [],
      },
      {
        title: 'Chapter 2',
        sections: [],
        storyEvents: [],
      },
    ],
  });

  const novel = (): NovelDto =>
    ({
      id: 'novel-id',
      title: 'Novel',
      compendiumIds: ['included-compendium'],
    }) as NovelDto;

  const compendium = (id: string): CompendiumDto => ({
    id,
    name: id,
    description: '',
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    records: [],
  });

  const createComponent = (): NovelEditorComponent => {
    TestBed.overrideProvider(ActivatedRoute, {
      useValue: {
        snapshot: {
          paramMap: convertToParamMap({ id: 'novel-id' }),
          queryParamMap: convertToParamMap(queryParams),
        },
      },
    });

    return TestBed.runInInjectionContext(() => new NovelEditorComponent());
  };

  beforeEach(() => {
    queryParams = {};
    localStorage.clear();
    novelService = jasmine.createSpyObj<NovelService>('NovelService', [
      'getNovel',
      'getNovelProse',
      'getFloatedMediaForNovel',
      'updateNovelProse',
    ]);
    promptService = jasmine.createSpyObj<PromptService>('PromptService', [
      'getPrompts',
    ]);
    compendiumService = jasmine.createSpyObj<CompendiumService>(
      'CompendiumService',
      ['getCompendia'],
    );
    toastrService = jasmine.createSpyObj<ToastrService>('ToastrService', [
      'success',
      'clear',
    ]);
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);

    novelService.getNovel.and.returnValue(of(novel()));
    novelService.getNovelProse.and.returnValue(of(createProse()));
    novelService.getFloatedMediaForNovel.and.returnValue([]);
    novelService.updateNovelProse.and.returnValue(of(undefined));
    promptService.getPrompts.and.returnValue(of([]));
    compendiumService.getCompendia.and.returnValue(of([]));
    toastrService.success.and.returnValue({
      toastId: 101,
    } as ReturnType<ToastrService['success']>);
    router.navigate.and.resolveTo(true);

    TestBed.configureTestingModule({
      providers: [
        { provide: NovelService, useValue: novelService },
        { provide: PromptService, useValue: promptService },
        { provide: CompendiumService, useValue: compendiumService },
        { provide: ToastrService, useValue: toastrService },
        { provide: Router, useValue: router },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ id: 'novel-id' }),
              queryParamMap: convertToParamMap({}),
            },
          },
        },
      ],
    });
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('loads the novel context and applies a valid chapter query', () => {
    queryParams = { chapter: '1' };
    const floatedMedia: CompendiumRecordMediaDto[] = [
      { id: 'media-id', url: '/media.webp', isCurrent: true, isVideo: false },
    ];
    novelService.getFloatedMediaForNovel.and.returnValue(floatedMedia);
    compendiumService.getCompendia.and.returnValue(
      of([
        compendium('excluded-compendium'),
        compendium('included-compendium'),
      ]),
    );
    component = createComponent();

    component.ngOnInit();

    expect(component.novelId).toBe('novel-id');
    expect(component.novel).toEqual(novel());
    expect(component.prose()).toEqual(createProse());
    expect(component.selectedChapterIndex).toBe(1);
    expect(component.chapters()).toEqual([
      { label: 'Chapter 1', value: 0 },
      { label: 'Chapter 2', value: 1 },
    ]);
    expect(component.compendia).toEqual([compendium('included-compendium')]);
    expect(component.prompts).toEqual([]);
    expect(component.floatedMedia).toBe(floatedMedia);
  });

  it('shows all chapters for the all-chapters query', () => {
    queryParams = { chapter: 'all' };
    component = createComponent();

    component.ngOnInit();

    expect(component.selectedChapterIndex).toBeNull();
  });

  it('defaults an out-of-range chapter query to the first chapter', () => {
    queryParams = { chapter: '99' };
    component = createComponent();

    component.ngOnInit();

    expect(component.selectedChapterIndex).toBe(0);
  });

  it('keeps chapter selection empty when the prose has no chapters', () => {
    novelService.getNovelProse.and.returnValue(of({ chapters: [] }));
    component = createComponent();

    component.ngOnInit();

    expect(component.selectedChapterIndex).toBeNull();
    expect(component.chapters()).toEqual([]);
  });

  it('persists prose changes and replaces the previous save toast', () => {
    component = createComponent();
    component.ngOnInit();
    const firstUpdate = createProse();
    const secondUpdate = createProse();
    secondUpdate.chapters[0].title = 'Updated chapter';
    toastrService.success.and.returnValues(
      { toastId: 101 } as ReturnType<ToastrService['success']>,
      { toastId: 102 } as ReturnType<ToastrService['success']>,
    );

    component.updateProse(firstUpdate);
    component.updateProse(secondUpdate);

    expect(novelService.updateNovelProse).toHaveBeenCalledWith(
      'novel-id',
      firstUpdate,
    );
    expect(novelService.updateNovelProse).toHaveBeenCalledWith(
      'novel-id',
      secondUpdate,
    );
    expect(component.prose()).toEqual(secondUpdate);
    expect(component.prose()).not.toBe(secondUpdate);
    expect(toastrService.success).toHaveBeenCalledTimes(2);
    expect(toastrService.clear).toHaveBeenCalledOnceWith(101);
  });

  it('writes chapter selections to the merged route query', () => {
    component = createComponent();
    component.ngOnInit();
    const route = TestBed.inject(ActivatedRoute);

    component.onChapterSelectionChange(null);
    component.onChapterSelectionChange(1);

    expect(router.navigate).toHaveBeenCalledWith([], {
      relativeTo: route,
      queryParams: { chapter: 'all' },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
    expect(router.navigate).toHaveBeenCalledWith([], {
      relativeTo: route,
      queryParams: { chapter: 1 },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  });
});
