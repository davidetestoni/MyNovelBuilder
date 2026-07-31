import { CommonModule } from '@angular/common';
import {
  Component,
  EventEmitter,
  Input,
  OnDestroy,
  Output,
  inject,
} from '@angular/core';
import { CdkDragDrop, DragDropModule } from '@angular/cdk/drag-drop';
import { Prose, StoryEvent } from '../../types/dtos/novel/prose';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import {
  StoryEventDialogComponent,
  StoryEventDialogData,
  StoryEventDialogResult,
} from '../story-event-dialog/story-event-dialog.component';
import {
  GenerateStoryEventsDialogComponent,
  GenerateStoryEventsDialogData,
  GenerateStoryEventsDialogResult,
} from '../generate-story-events-dialog/generate-story-events-dialog.component';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';

interface StorylineTimelineEvent extends StoryEvent {
  chapterTitle: string;
  chapterIndex: number;
  storyEventIndex: number;
}

interface StorylineChapterGroup {
  chapterTitle: string;
  chapterIndex: number;
  events: StorylineTimelineEvent[];
}

interface StorylineEventReference {
  chapterIndex: number;
  storyEventIndex: number;
}

interface StorylineEventCreateRequest {
  chapterIndex: number;
  storyEvent: StoryEvent;
}

interface StorylineEventUpdateRequest {
  chapterIndex: number;
  storyEventIndex: number;
  storyEvent: StoryEvent;
}

interface StorylineEventReorderRequest {
  previousChapterIndex: number;
  currentChapterIndex: number;
  previousIndex: number;
  currentIndex: number;
}

@Component({
  selector: 'app-storyline',
  standalone: true,
  imports: [CommonModule, ConfirmDialogModule, DragDropModule],
  providers: [DialogService, ConfirmationService],
  templateUrl: './storyline.component.html',
  styleUrl: './storyline.component.scss',
})
export class StorylineComponent implements OnDestroy {
  private dialogService = inject(DialogService);
  private confirmationService = inject(ConfirmationService);
  private dialogRef: DynamicDialogRef | null = null;

  @Input() prose: Prose | null = null;
  @Input() selectedChapterIndex: number | null = null;
  @Input() prompts: PromptDto[] | null = null;
  @Input() novelId!: string;
  @Output() chapterSelected = new EventEmitter<number>();
  @Output() storyEventRemoved = new EventEmitter<StorylineEventReference>();
  @Output() storyEventCreated = new EventEmitter<StorylineEventCreateRequest>();
  @Output() storyEventUpdated = new EventEmitter<StorylineEventUpdateRequest>();
  @Output() storyEventsReordered =
    new EventEmitter<StorylineEventReorderRequest>();
  @Output() storyEventsGenerated = new EventEmitter<
    { chapterIndex: number; storyEvents: StoryEvent[] }[]
  >();

  get chapterGroups(): StorylineChapterGroup[] {
    if (!this.prose) {
      return [];
    }

    return this.prose.chapters.map((chapter, chapterIndex) => {
      const events = (chapter.storyEvents || []).map(
        (storyEvent, storyEventIndex) => ({
          title: storyEvent.title?.trim() || 'Story Event',
          date: storyEvent.date?.trim() || '',
          description: storyEvent.description?.trim() || '',
          chapterTitle: chapter.title,
          chapterIndex,
          storyEventIndex,
        }),
      );

      return {
        chapterTitle: chapter.title,
        chapterIndex,
        events,
      };
    });
  }

  get canCreateStoryEvent(): boolean {
    return (this.prose?.chapters.length || 0) > 0;
  }

  ngOnDestroy(): void {
    if (this.dialogRef) {
      this.dialogRef.close();
      this.dialogRef = null;
    }
  }

  selectChapter(event: StorylineTimelineEvent): void {
    this.chapterSelected.emit(event.chapterIndex);
  }

  selectChapterByIndex(chapterIndex: number): void {
    this.chapterSelected.emit(chapterIndex);
  }

  onStoryEventDrop(
    event: CdkDragDrop<StorylineTimelineEvent[]>,
    currentChapterIndex: number,
  ): void {
    const previousChapterIndex = event.previousContainer.data?.[0]
      ?.chapterIndex;
    const sourceChapterIndex =
      previousChapterIndex ?? event.item.data?.chapterIndex;

    if (
      sourceChapterIndex === undefined ||
      (sourceChapterIndex === currentChapterIndex &&
        event.previousIndex === event.currentIndex)
    ) {
      return;
    }

    this.storyEventsReordered.emit({
      previousChapterIndex: sourceChapterIndex,
      currentChapterIndex,
      previousIndex: event.previousIndex,
      currentIndex: event.currentIndex,
    });
  }

  selectChapterFromKeyboard(
    event: StorylineTimelineEvent,
    keyboardEvent: KeyboardEvent,
  ): void {
    if (keyboardEvent.key !== 'Enter' && keyboardEvent.key !== ' ') {
      return;
    }

    keyboardEvent.preventDefault();
    this.selectChapter(event);
  }

  removeStoryEvent(
    event: StorylineTimelineEvent,
    interactionEvent: Event,
  ): void {
    interactionEvent.stopPropagation();
    interactionEvent.preventDefault();

    this.confirmationService.confirm({
      message:
        'Are you sure you want to remove this story event? This action cannot be undone.',
      header: 'Confirm Story Event Removal',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.storyEventRemoved.emit({
          chapterIndex: event.chapterIndex,
          storyEventIndex: event.storyEventIndex,
        });
      },
    });
  }

  openCreateStoryEventDialog(interactionEvent?: Event): void {
    if (interactionEvent) {
      interactionEvent.stopPropagation();
      interactionEvent.preventDefault();
    }

    const chapterIndex = this.getTargetChapterIndex();
    if (chapterIndex === null) {
      return;
    }

    this.openStoryEventDialog(
      {
        mode: 'create',
        chapters: this.getDialogChapters(),
        selectedChapterIndex: chapterIndex,
      },
      (result) => {
        this.storyEventCreated.emit({
          chapterIndex: result.chapterIndex,
          storyEvent: result.storyEvent,
        });
      },
    );
  }

  openEditStoryEventDialog(
    event: StorylineTimelineEvent,
    interactionEvent: Event,
  ): void {
    interactionEvent.stopPropagation();
    interactionEvent.preventDefault();

    this.openStoryEventDialog(
      {
        mode: 'edit',
        chapters: this.getDialogChapters(),
        selectedChapterIndex: event.chapterIndex,
        storyEvent: {
          title: event.title,
          date: event.date,
          description: event.description,
        },
      },
      (result) => {
        this.storyEventUpdated.emit({
          chapterIndex: event.chapterIndex,
          storyEventIndex: event.storyEventIndex,
          storyEvent: result.storyEvent,
        });
      },
    );
  }

  openGenerateStoryEventsDialog(interactionEvent?: Event): void {
    if (interactionEvent) {
      interactionEvent.stopPropagation();
      interactionEvent.preventDefault();
    }

    if (!this.canCreateStoryEvent) {
      return;
    }

    const data: GenerateStoryEventsDialogData = {
      chapters: this.getDialogChapters(),
      selectedChapterIndex: this.selectedChapterIndex,
      prompts: this.prompts ?? [],
      novelId: this.novelId,
    };

    const dialogRef = this.dialogService.open(
      GenerateStoryEventsDialogComponent,
      {
        header: 'Generate Story Events',
        width: '45vw',
        contentStyle: { overflow: 'auto' },
        baseZIndex: 10000,
        closable: true,
        closeOnEscape: true,
        modal: true,
        dismissableMask: true,
        data,
      },
    );

    this.dialogRef = dialogRef;
    dialogRef?.onClose.subscribe(
      (result: GenerateStoryEventsDialogResult | undefined) => {
        if (this.dialogRef === dialogRef) {
          this.dialogRef = null;
        }

        if (!result || !result.chapters.length) {
          return;
        }

        this.storyEventsGenerated.emit(result.chapters);
      },
    );
  }

  private getTargetChapterIndex(): number | null {
    if (!this.canCreateStoryEvent) {
      return null;
    }

    return (this.prose?.chapters.length || 1) - 1;
  }

  private openStoryEventDialog(
    data: StoryEventDialogData,
    onClose: (result: StoryEventDialogResult) => void,
  ): void {
    const dialogRef = this.dialogService.open(StoryEventDialogComponent, {
      header: data.mode === 'edit' ? 'Edit Story Event' : 'Create Story Event',
      width: '45vw',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      closable: true,
      closeOnEscape: true,
      modal: true,
      dismissableMask: true,
      data,
    });

    this.dialogRef = dialogRef;
    dialogRef?.onClose.subscribe(
      (result: StoryEventDialogResult | undefined) => {
        if (this.dialogRef === dialogRef) {
          this.dialogRef = null;
        }

        if (!result) {
          return;
        }

        onClose(result);
      },
    );
  }

  private getDialogChapters(): { label: string; value: number }[] {
    if (!this.prose) {
      return [];
    }

    return this.prose.chapters.map((chapter, chapterIndex) => ({
      label: chapter.title,
      value: chapterIndex,
    }));
  }

  getDropListId(chapterIndex: number): string {
    return `storyline-drop-list-${chapterIndex}`;
  }

  getDropListIds(): string[] {
    if (!this.prose) {
      return [];
    }

    return this.prose.chapters.map((_, chapterIndex) =>
      this.getDropListId(chapterIndex),
    );
  }
}
