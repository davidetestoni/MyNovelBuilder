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
import type { Prose, StoryEvent } from '../../types/dtos/novel/prose';
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

  const storyEvent = (title: string): StoryEvent => ({
    title,
    date: '2026-01-01',
    description: `${title} description`,
  });

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
      ['getNovelCompendia'],
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
    compendiumService.getNovelCompendia.and.returnValue(of([]));
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
    compendiumService.getNovelCompendia.and.returnValue(
      of([compendium('included-compendium')]),
    );
    component = createComponent();

    component.ngOnInit();

    expect(component.novelId).toBe('novel-id');
    expect(component.novel).toEqual(novel());
    expect(component.prose()).toEqual(createProse());
    expect(component.selectedChapterIndex).toBe(1);
    expect(component.chapters()).toEqual([
      { label: '1. Chapter 1', value: 0 },
      { label: '2. Chapter 2', value: 1 },
    ]);
    expect(component.compendia).toEqual([compendium('included-compendium')]);
    expect(compendiumService.getNovelCompendia).toHaveBeenCalledOnceWith(
      'novel-id',
    );
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

  it('creates a story event without mutating the loaded prose', () => {
    const initialProse = createProse();
    const createdEvent = storyEvent('Created');
    novelService.getNovelProse.and.returnValue(of(initialProse));
    component = createComponent();
    component.ngOnInit();

    component.createStoryEvent({
      chapterIndex: 0,
      storyEvent: createdEvent,
    });

    const updatedProse =
      novelService.updateNovelProse.calls.mostRecent().args[1];
    expect(updatedProse.chapters[0].storyEvents).toEqual([createdEvent]);
    expect(updatedProse.chapters[0]).not.toBe(initialProse.chapters[0]);
    expect(updatedProse.chapters[1]).toBe(initialProse.chapters[1]);
    expect(initialProse.chapters[0].storyEvents).toEqual([]);
  });

  it('updates a story event without mutating the loaded prose', () => {
    const initialEvent = storyEvent('Original');
    const replacementEvent = storyEvent('Replacement');
    const initialProse = createProse();
    initialProse.chapters[0].storyEvents = [initialEvent];
    novelService.getNovelProse.and.returnValue(of(initialProse));
    component = createComponent();
    component.ngOnInit();

    component.updateStoryEvent({
      chapterIndex: 0,
      storyEventIndex: 0,
      storyEvent: replacementEvent,
    });

    const updatedProse =
      novelService.updateNovelProse.calls.mostRecent().args[1];
    expect(updatedProse.chapters[0].storyEvents).toEqual([
      replacementEvent,
    ]);
    expect(updatedProse.chapters[0].storyEvents).not.toBe(
      initialProse.chapters[0].storyEvents,
    );
    expect(initialProse.chapters[0].storyEvents).toEqual([initialEvent]);
  });

  it('removes a story event without mutating the loaded prose', () => {
    const retainedEvent = storyEvent('Retained');
    const removedEvent = storyEvent('Removed');
    const initialProse = createProse();
    initialProse.chapters[0].storyEvents = [retainedEvent, removedEvent];
    novelService.getNovelProse.and.returnValue(of(initialProse));
    component = createComponent();
    component.ngOnInit();

    component.removeStoryEvent({
      chapterIndex: 0,
      storyEventIndex: 1,
    });

    const updatedProse =
      novelService.updateNovelProse.calls.mostRecent().args[1];
    expect(updatedProse.chapters[0].storyEvents).toEqual([retainedEvent]);
    expect(initialProse.chapters[0].storyEvents).toEqual([
      retainedEvent,
      removedEvent,
    ]);
  });

  it('ignores invalid story-event mutations', () => {
    component = createComponent();
    component.ngOnInit();

    component.createStoryEvent({
      chapterIndex: 99,
      storyEvent: storyEvent('Created'),
    });
    component.updateStoryEvent({
      chapterIndex: 0,
      storyEventIndex: 99,
      storyEvent: storyEvent('Updated'),
    });
    component.removeStoryEvent({
      chapterIndex: 0,
      storyEventIndex: 99,
    });

    expect(novelService.updateNovelProse).not.toHaveBeenCalled();
  });

  it('reorders story events within one chapter immutably', () => {
    const firstEvent = storyEvent('First');
    const secondEvent = storyEvent('Second');
    const thirdEvent = storyEvent('Third');
    const initialProse = createProse();
    initialProse.chapters[0].storyEvents = [
      firstEvent,
      secondEvent,
      thirdEvent,
    ];
    novelService.getNovelProse.and.returnValue(of(initialProse));
    component = createComponent();
    component.ngOnInit();

    component.reorderStoryEvents({
      previousChapterIndex: 0,
      currentChapterIndex: 0,
      previousIndex: 0,
      currentIndex: 2,
    });

    const updatedProse =
      novelService.updateNovelProse.calls.mostRecent().args[1];
    expect(updatedProse.chapters[0].storyEvents).toEqual([
      secondEvent,
      thirdEvent,
      firstEvent,
    ]);
    expect(initialProse.chapters[0].storyEvents).toEqual([
      firstEvent,
      secondEvent,
      thirdEvent,
    ]);
  });

  it('moves story events between chapters immutably', () => {
    const firstEvent = storyEvent('First');
    const movedEvent = storyEvent('Moved');
    const destinationEvent = storyEvent('Destination');
    const initialProse = createProse();
    initialProse.chapters[0].storyEvents = [firstEvent, movedEvent];
    initialProse.chapters[1].storyEvents = [destinationEvent];
    novelService.getNovelProse.and.returnValue(of(initialProse));
    component = createComponent();
    component.ngOnInit();

    component.reorderStoryEvents({
      previousChapterIndex: 0,
      currentChapterIndex: 1,
      previousIndex: 1,
      currentIndex: 1,
    });

    const updatedProse =
      novelService.updateNovelProse.calls.mostRecent().args[1];
    expect(updatedProse.chapters[0].storyEvents).toEqual([firstEvent]);
    expect(updatedProse.chapters[1].storyEvents).toEqual([
      destinationEvent,
      movedEvent,
    ]);
    expect(initialProse.chapters[0].storyEvents).toEqual([
      firstEvent,
      movedEvent,
    ]);
    expect(initialProse.chapters[1].storyEvents).toEqual([
      destinationEvent,
    ]);
  });

  it('ignores invalid and unchanged story-event reorders', () => {
    const initialProse = createProse();
    initialProse.chapters[0].storyEvents = [storyEvent('First')];
    novelService.getNovelProse.and.returnValue(of(initialProse));
    component = createComponent();
    component.ngOnInit();

    component.reorderStoryEvents({
      previousChapterIndex: 0,
      currentChapterIndex: 0,
      previousIndex: 0,
      currentIndex: 0,
    });
    component.reorderStoryEvents({
      previousChapterIndex: 99,
      currentChapterIndex: 0,
      previousIndex: 0,
      currentIndex: 0,
    });
    component.reorderStoryEvents({
      previousChapterIndex: 0,
      currentChapterIndex: 1,
      previousIndex: 0,
      currentIndex: 99,
    });

    expect(novelService.updateNovelProse).not.toHaveBeenCalled();
    expect(component.prose()).toBe(initialProse);
  });

  it('appends generated story events without mutating existing chapters', () => {
    const existingEvent = storyEvent('Existing');
    const generatedEvent = storyEvent('Generated');
    const initialProse = createProse();
    initialProse.chapters[0].storyEvents = [existingEvent];
    novelService.getNovelProse.and.returnValue(of(initialProse));
    component = createComponent();
    component.ngOnInit();

    component.addGeneratedStoryEvents([
      {
        chapterIndex: 0,
        storyEvents: [generatedEvent],
      },
    ]);

    const updatedProse =
      novelService.updateNovelProse.calls.mostRecent().args[1];
    expect(updatedProse.chapters[0].storyEvents).toEqual([
      existingEvent,
      generatedEvent,
    ]);
    expect(updatedProse.chapters[0]).not.toBe(initialProse.chapters[0]);
    expect(updatedProse.chapters[1]).toBe(initialProse.chapters[1]);
    expect(initialProse.chapters[0].storyEvents).toEqual([existingEvent]);
  });

  it('applies valid generated events while ignoring invalid chapters', () => {
    const generatedEvent = storyEvent('Generated');
    component = createComponent();
    component.ngOnInit();

    component.addGeneratedStoryEvents([
      { chapterIndex: -1, storyEvents: [storyEvent('Negative')] },
      { chapterIndex: 0.5, storyEvents: [storyEvent('Fractional')] },
      { chapterIndex: 99, storyEvents: [storyEvent('Out of range')] },
      { chapterIndex: 1, storyEvents: [generatedEvent] },
    ]);

    const updatedProse =
      novelService.updateNovelProse.calls.mostRecent().args[1];
    expect(updatedProse.chapters[0].storyEvents).toEqual([]);
    expect(updatedProse.chapters[1].storyEvents).toEqual([generatedEvent]);
  });

  it('ignores generated-event batches without a valid chapter', () => {
    component = createComponent();
    component.ngOnInit();

    component.addGeneratedStoryEvents([]);
    component.addGeneratedStoryEvents([
      { chapterIndex: -1, storyEvents: [storyEvent('Negative')] },
      { chapterIndex: 99, storyEvents: [storyEvent('Out of range')] },
    ]);

    expect(novelService.updateNovelProse).not.toHaveBeenCalled();
  });

  it('does not persist generated-event batches with no events', () => {
    const initialProse = createProse();
    novelService.getNovelProse.and.returnValue(of(initialProse));
    component = createComponent();
    component.ngOnInit();

    component.addGeneratedStoryEvents([
      { chapterIndex: 0, storyEvents: [] },
      { chapterIndex: 1, storyEvents: [] },
    ]);

    expect(novelService.updateNovelProse).not.toHaveBeenCalled();
    expect(component.prose()).toBe(initialProse);
  });
});
