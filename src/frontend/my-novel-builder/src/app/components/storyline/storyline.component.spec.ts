import type { CdkDragDrop } from '@angular/cdk/drag-drop';
import { TestBed } from '@angular/core/testing';
import { ConfirmationService } from 'primeng/api';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { Subject } from 'rxjs';
import type { Prose } from '../../types/dtos/novel/prose';
import { GenerateStoryEventsDialogComponent } from '../generate-story-events-dialog/generate-story-events-dialog.component';
import { StoryEventDialogComponent } from '../story-event-dialog/story-event-dialog.component';
import { StorylineComponent } from './storyline.component';

describe('StorylineComponent workflow', () => {
  let component: StorylineComponent;
  let dialogService: jasmine.SpyObj<DialogService>;
  let confirmationService: jasmine.SpyObj<ConfirmationService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let dialogClosed: Subject<unknown>;

  const prose = (): Prose => ({
    chapters: [
      {
        title: 'Opening',
        sections: [],
        storyEvents: [
          {
            title: '  Arrival  ',
            date: '  Dawn  ',
            description: '  The hero arrives.  ',
          },
          {
            title: '',
            date: '',
            description: '',
          },
        ],
      },
      {
        title: 'Reckoning',
        sections: [],
        storyEvents: [],
      },
    ],
  });

  const interactionEvent = (): jasmine.SpyObj<Event> =>
    jasmine.createSpyObj<Event>('Event', [
      'stopPropagation',
      'preventDefault',
    ]);

  const createDialogRef = (): jasmine.SpyObj<DynamicDialogRef> => {
    dialogClosed = new Subject<unknown>();
    return jasmine.createSpyObj<DynamicDialogRef>(
      'DynamicDialogRef',
      ['close'],
      { onClose: dialogClosed.asObservable() },
    );
  };

  beforeEach(() => {
    dialogService = jasmine.createSpyObj<DialogService>('DialogService', [
      'open',
    ]);
    confirmationService = jasmine.createSpyObj<ConfirmationService>(
      'ConfirmationService',
      ['confirm'],
    );
    dialogRef = createDialogRef();
    dialogService.open.and.returnValue(dialogRef);

    TestBed.configureTestingModule({
      providers: [
        { provide: DialogService, useValue: dialogService },
        { provide: ConfirmationService, useValue: confirmationService },
      ],
    });

    component = TestBed.runInInjectionContext(() => new StorylineComponent());
    component.prose = prose();
    component.selectedChapterIndex = 0;
    component.prompts = [];
    component.novelId = 'novel-one';
  });

  it('maps chapters and normalized events into timeline groups', () => {
    expect(component.chapterGroups).toEqual([
      {
        chapterTitle: 'Opening',
        chapterIndex: 0,
        events: [
          {
            title: 'Arrival',
            date: 'Dawn',
            description: 'The hero arrives.',
            chapterTitle: 'Opening',
            chapterIndex: 0,
            storyEventIndex: 0,
          },
          {
            title: 'Story Event',
            date: '',
            description: '',
            chapterTitle: 'Opening',
            chapterIndex: 0,
            storyEventIndex: 1,
          },
        ],
      },
      {
        chapterTitle: 'Reckoning',
        chapterIndex: 1,
        events: [],
      },
    ]);
  });

  it('returns no groups and disables creation without prose chapters', () => {
    component.prose = null;
    expect(component.chapterGroups).toEqual([]);
    expect(component.canCreateStoryEvent).toBeFalse();

    component.prose = { chapters: [] };
    expect(component.canCreateStoryEvent).toBeFalse();
  });

  it('emits chapter selections from events and chapter chips', () => {
    spyOn(component.chapterSelected, 'emit');
    const event = component.chapterGroups[0].events[0];

    component.selectChapter(event);
    component.selectChapterByIndex(1);

    expect(component.chapterSelected.emit).toHaveBeenCalledWith(0);
    expect(component.chapterSelected.emit).toHaveBeenCalledWith(1);
  });

  it('handles Enter and Space keyboard selection only', () => {
    spyOn(component.chapterSelected, 'emit');
    const timelineEvent = component.chapterGroups[0].events[0];
    const enter = jasmine.createSpyObj<KeyboardEvent>('KeyboardEvent', [
      'preventDefault',
    ]);
    Object.defineProperty(enter, 'key', { value: 'Enter' });
    const space = jasmine.createSpyObj<KeyboardEvent>('KeyboardEvent', [
      'preventDefault',
    ]);
    Object.defineProperty(space, 'key', { value: ' ' });
    const escape = jasmine.createSpyObj<KeyboardEvent>('KeyboardEvent', [
      'preventDefault',
    ]);
    Object.defineProperty(escape, 'key', { value: 'Escape' });

    component.selectChapterFromKeyboard(timelineEvent, enter);
    component.selectChapterFromKeyboard(timelineEvent, space);
    component.selectChapterFromKeyboard(timelineEvent, escape);

    expect(component.chapterSelected.emit).toHaveBeenCalledTimes(2);
    expect(enter.preventDefault).toHaveBeenCalled();
    expect(space.preventDefault).toHaveBeenCalled();
    expect(escape.preventDefault).not.toHaveBeenCalled();
  });

  it('emits an intra-chapter reorder and ignores a no-op', () => {
    spyOn(component.storyEventsReordered, 'emit');
    const events = component.chapterGroups[0].events;
    const drop = {
      previousContainer: { data: events },
      item: { data: events[1] },
      previousIndex: 1,
      currentIndex: 0,
    } as unknown as CdkDragDrop<typeof events>;

    component.onStoryEventDrop(drop, 0);
    component.onStoryEventDrop(
      {
        ...drop,
        previousIndex: 0,
        currentIndex: 0,
      } as CdkDragDrop<typeof events>,
      0,
    );

    expect(component.storyEventsReordered.emit).toHaveBeenCalledOnceWith({
      previousChapterIndex: 0,
      currentChapterIndex: 0,
      previousIndex: 1,
      currentIndex: 0,
    });
  });

  it('emits a cross-chapter reorder using the dragged item for an empty source', () => {
    spyOn(component.storyEventsReordered, 'emit');
    const timelineEvent = component.chapterGroups[0].events[0];

    component.onStoryEventDrop(
      {
        previousContainer: { data: [] },
        item: { data: timelineEvent },
        previousIndex: 0,
        currentIndex: 0,
      } as unknown as CdkDragDrop<typeof component.chapterGroups[0]['events']>,
      1,
    );

    expect(component.storyEventsReordered.emit).toHaveBeenCalledOnceWith({
      previousChapterIndex: 0,
      currentChapterIndex: 1,
      previousIndex: 0,
      currentIndex: 0,
    });
  });

  it('ignores a drop with no resolvable source chapter', () => {
    spyOn(component.storyEventsReordered, 'emit');

    component.onStoryEventDrop(
      {
        previousContainer: { data: [] },
        item: { data: undefined },
        previousIndex: 0,
        currentIndex: 0,
      } as unknown as CdkDragDrop<
        typeof component.chapterGroups[0]['events']
      >,
      1,
    );

    expect(component.storyEventsReordered.emit).not.toHaveBeenCalled();
  });

  it('confirms removal and emits only when accepted', () => {
    spyOn(component.storyEventRemoved, 'emit');
    const event = component.chapterGroups[0].events[1];
    const interaction = interactionEvent();

    component.removeStoryEvent(event, interaction);

    expect(interaction.stopPropagation).toHaveBeenCalled();
    expect(interaction.preventDefault).toHaveBeenCalled();
    expect(confirmationService.confirm).toHaveBeenCalledTimes(1);
    expect(component.storyEventRemoved.emit).not.toHaveBeenCalled();

    const confirmation = confirmationService.confirm.calls.mostRecent().args[0];
    confirmation.accept?.();
    expect(component.storyEventRemoved.emit).toHaveBeenCalledOnceWith({
      chapterIndex: 0,
      storyEventIndex: 1,
    });
  });

  it('opens create on the last chapter and emits the dialog result', () => {
    spyOn(component.storyEventCreated, 'emit');
    const interaction = interactionEvent();

    component.openCreateStoryEventDialog(interaction);

    expect(interaction.stopPropagation).toHaveBeenCalled();
    expect(interaction.preventDefault).toHaveBeenCalled();
    expect(dialogService.open).toHaveBeenCalledOnceWith(
      StoryEventDialogComponent,
      jasmine.objectContaining({
        header: 'Create Story Event',
        data: {
          mode: 'create',
          chapters: [
            { label: 'Opening', value: 0 },
            { label: 'Reckoning', value: 1 },
          ],
          selectedChapterIndex: 1,
        },
      }),
    );

    const result = {
      chapterIndex: 0,
      storyEvent: {
        title: 'Created',
        date: 'Noon',
        description: 'A new event.',
      },
    };
    dialogClosed.next(result);
    expect(component.storyEventCreated.emit).toHaveBeenCalledOnceWith(result);
  });

  it('does not open create without chapters', () => {
    component.prose = { chapters: [] };
    component.openCreateStoryEventDialog();
    expect(dialogService.open).not.toHaveBeenCalled();
  });

  it('opens edit with normalized event data and preserves its location', () => {
    spyOn(component.storyEventUpdated, 'emit');
    const event = component.chapterGroups[0].events[0];
    const interaction = interactionEvent();

    component.openEditStoryEventDialog(event, interaction);

    expect(dialogService.open).toHaveBeenCalledOnceWith(
      StoryEventDialogComponent,
      jasmine.objectContaining({
        header: 'Edit Story Event',
        data: jasmine.objectContaining({
          mode: 'edit',
          selectedChapterIndex: 0,
          storyEvent: {
            title: 'Arrival',
            date: 'Dawn',
            description: 'The hero arrives.',
          },
        }),
      }),
    );

    dialogClosed.next({
      chapterIndex: 1,
      storyEvent: {
        title: 'Updated',
        date: 'Dusk',
        description: 'The event changes.',
      },
    });
    expect(component.storyEventUpdated.emit).toHaveBeenCalledOnceWith({
      chapterIndex: 0,
      storyEventIndex: 0,
      storyEvent: {
        title: 'Updated',
        date: 'Dusk',
        description: 'The event changes.',
      },
    });
  });

  it('ignores an empty create or edit dialog result', () => {
    spyOn(component.storyEventCreated, 'emit');
    component.openCreateStoryEventDialog();
    dialogClosed.next(undefined);
    expect(component.storyEventCreated.emit).not.toHaveBeenCalled();
  });

  it('opens generated events with current context and emits non-empty results', () => {
    spyOn(component.storyEventsGenerated, 'emit');
    const interaction = interactionEvent();

    component.openGenerateStoryEventsDialog(interaction);

    expect(dialogService.open).toHaveBeenCalledOnceWith(
      GenerateStoryEventsDialogComponent,
      jasmine.objectContaining({
        header: 'Generate Story Events',
        data: {
          chapters: [
            { label: 'Opening', value: 0 },
            { label: 'Reckoning', value: 1 },
          ],
          selectedChapterIndex: 0,
          prompts: [],
          novelId: 'novel-one',
        },
      }),
    );

    const chapters = [
      {
        chapterIndex: 1,
        storyEvents: [
          { title: 'Generated', date: '', description: 'Generated event' },
        ],
      },
    ];
    dialogClosed.next({ chapters });
    expect(component.storyEventsGenerated.emit).toHaveBeenCalledOnceWith(
      chapters,
    );
  });

  it('ignores missing or empty generated results', () => {
    spyOn(component.storyEventsGenerated, 'emit');
    component.openGenerateStoryEventsDialog();
    dialogClosed.next(undefined);
    dialogClosed.next({ chapters: [] });
    expect(component.storyEventsGenerated.emit).not.toHaveBeenCalled();
  });

  it('does not open generation without chapters', () => {
    component.prose = null;
    component.openGenerateStoryEventsDialog();
    expect(dialogService.open).not.toHaveBeenCalled();
  });

  it('uses empty prompts when none were supplied', () => {
    component.prompts = null;
    component.openGenerateStoryEventsDialog();

    const options = dialogService.open.calls.mostRecent().args[1] as {
      data: { prompts: unknown[] };
    };
    expect(options.data.prompts).toEqual([]);
  });

  it('creates stable drop-list identifiers', () => {
    expect(component.getDropListId(4)).toBe('storyline-drop-list-4');
    expect(component.getDropListIds()).toEqual([
      'storyline-drop-list-0',
      'storyline-drop-list-1',
    ]);

    component.prose = null;
    expect(component.getDropListIds()).toEqual([]);
  });

  it('closes an active dialog on destruction', () => {
    component.openCreateStoryEventDialog();

    component.ngOnDestroy();
    component.ngOnDestroy();

    expect(dialogRef.close).toHaveBeenCalledTimes(1);
  });

  it('does not close an already completed dialog on destruction', () => {
    component.openCreateStoryEventDialog();
    dialogClosed.next(undefined);

    component.ngOnDestroy();

    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('does not let an older dialog completion clear a newer dialog reference', () => {
    const firstClosed = dialogClosed;
    const secondRef = createDialogRef();
    const secondClosed = dialogClosed;
    dialogService.open.and.returnValues(dialogRef, secondRef);

    component.openCreateStoryEventDialog();
    component.openGenerateStoryEventsDialog();
    firstClosed.next(undefined);
    component.ngOnDestroy();

    expect(dialogRef.close).not.toHaveBeenCalled();
    expect(secondRef.close).toHaveBeenCalledOnceWith();
    secondClosed.complete();
  });
});
