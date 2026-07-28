import { TestBed } from '@angular/core/testing';
import { ConfirmationService } from 'primeng/api';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { of } from 'rxjs';
import { NovelService } from '../../services/novel.service';
import type { NovelDto } from '../../types/dtos/novel/novel.dto';
import type { Prose, Section } from '../../types/dtos/novel/prose';
import { StoryPlannerComponent } from './story-planner.component';

describe('StoryPlannerComponent workflows', () => {
  let component: StoryPlannerComponent;
  let novelService: jasmine.SpyObj<NovelService>;
  let router: jasmine.SpyObj<Router>;
  let toastrService: jasmine.SpyObj<ToastrService>;

  const novel = (): NovelDto =>
    ({
      id: 'novel-id',
      title: 'Novel',
    }) as NovelDto;

  const section = (
    summary = 'Section summary',
    text = '<p>Section text</p>',
  ): Section => ({
    summary,
    text,
    images: [],
    recordOverrides: [],
  });

  const prose = (): Prose => ({
    chapters: [
      {
        title: 'Chapter 1',
        sections: [section()],
        storyEvents: [],
      },
      {
        title: 'Chapter 2',
        sections: [],
        storyEvents: [],
      },
    ],
  });

  beforeEach(() => {
    novelService = jasmine.createSpyObj<NovelService>('NovelService', [
      'getNovel',
      'getNovelProse',
      'updateNovelProse',
    ]);
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    toastrService = jasmine.createSpyObj<ToastrService>('ToastrService', [
      'success',
      'clear',
    ]);
    const confirmationService =
      jasmine.createSpyObj<ConfirmationService>('ConfirmationService', [
        'confirm',
      ]);

    novelService.getNovel.and.returnValue(of(novel()));
    novelService.getNovelProse.and.returnValue(of(prose()));
    novelService.updateNovelProse.and.returnValue(of(undefined));
    router.navigate.and.resolveTo(true);
    toastrService.success.and.returnValue({
      toastId: 101,
    } as ReturnType<ToastrService['success']>);

    TestBed.configureTestingModule({
      providers: [
        { provide: NovelService, useValue: novelService },
        { provide: Router, useValue: router },
        { provide: ToastrService, useValue: toastrService },
        { provide: ConfirmationService, useValue: confirmationService },
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
      () => new StoryPlannerComponent(),
    );
  });

  it('loads the novel and prose from the route context', () => {
    const loadedNovel = novel();
    const loadedProse = prose();
    novelService.getNovel.and.returnValue(of(loadedNovel));
    novelService.getNovelProse.and.returnValue(of(loadedProse));

    component.ngOnInit();

    expect(component.novelId).toBe('novel-id');
    expect(component.novel).toBe(loadedNovel);
    expect(component.prose).toBe(loadedProse);
    expect(novelService.getNovel).toHaveBeenCalledOnceWith('novel-id');
    expect(novelService.getNovelProse).toHaveBeenCalledOnceWith(
      'novel-id',
    );
  });

  it('navigates to prose and exposes stable section drop-list IDs', async () => {
    component.ngOnInit();

    expect(component.sectionDropListIds).toEqual([
      'chapter-sections-0',
      'chapter-sections-1',
    ]);
    expect(component.getSectionDropListId(3)).toBe('chapter-sections-3');

    await component.goToProse();

    expect(router.navigate).toHaveBeenCalledOnceWith([
      '/novel',
      'novel-id',
    ]);
  });

  it('adds and persists a chapter with the expected defaults', () => {
    component.ngOnInit();

    component.addChapter();

    expect(component.prose?.chapters[2]).toEqual({
      title: 'Chapter 3',
      sections: [],
      storyEvents: [],
    });
    expect(novelService.updateNovelProse).toHaveBeenCalledOnceWith(
      'novel-id',
      component.prose!,
    );
    expect(toastrService.success).toHaveBeenCalledTimes(1);
  });

  it('adds and persists a section with the expected defaults', () => {
    component.ngOnInit();

    component.addSection(1);

    expect(component.prose?.chapters[1].sections).toEqual([
      {
        summary: '[Missing summary]',
        text: '',
        images: [],
        recordOverrides: [],
      },
    ]);
    expect(novelService.updateNovelProse).toHaveBeenCalledOnceWith(
      'novel-id',
      component.prose!,
    );
  });

  it('trims and persists a changed chapter title only once', () => {
    component.ngOnInit();
    const titleElement = document.createElement('h3');
    titleElement.innerText = '  Updated chapter  ';
    const event = { target: titleElement } as unknown as Event;

    component.updateChapterTitle(0, event);
    titleElement.innerText = 'Updated chapter';
    component.updateChapterTitle(0, event);

    expect(component.prose?.chapters[0].title).toBe('Updated chapter');
    expect(novelService.updateNovelProse).toHaveBeenCalledTimes(1);
  });

  it('replaces and persists a blank chapter title', () => {
    component.ngOnInit();
    const titleElement = document.createElement('h3');
    titleElement.innerText = '   ';

    component.updateChapterTitle(0, {
      target: titleElement,
    } as unknown as Event);

    expect(component.prose?.chapters[0].title).toBe('[Untitled chapter]');
    expect(titleElement.innerText).toBe('[Untitled chapter]');
    expect(novelService.updateNovelProse).toHaveBeenCalledTimes(1);
  });

  it('prefers summaries, then plain text, then a placeholder for previews', () => {
    expect(
      component.getSectionPreview(
        section('  Summary takes priority  ', '<p>Ignored text</p>'),
      ),
    ).toBe('Summary takes priority');
    expect(
      component.getSectionPreview(
        section('[Missing summary]', '<p>Text <strong>preview</strong></p>'),
      ),
    ).toBe('Text preview');
    expect(
      component.getSectionPreview(section(' ', '<p> &nbsp; </p>')),
    ).toBe('No summary or text yet.');
  });
});
