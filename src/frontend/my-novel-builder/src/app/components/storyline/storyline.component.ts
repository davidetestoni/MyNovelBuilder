import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { Prose, StoryEvent } from '../../types/dtos/novel/prose';

interface StorylineTimelineEvent extends StoryEvent {
  chapterTitle: string;
  chapterIndex: number;
  storyEventIndex: number;
}

@Component({
  selector: 'app-storyline',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './storyline.component.html',
  styleUrl: './storyline.component.scss',
})
export class StorylineComponent {
  @Input() prose: Prose | null = null;
  @Output() chapterSelected = new EventEmitter<number>();

  get events(): StorylineTimelineEvent[] {
    if (!this.prose) {
      return [];
    }

    const events: StorylineTimelineEvent[] = [];
    this.prose.chapters.forEach((chapter, chapterIndex) => {
      (chapter.storyEvents || []).forEach((storyEvent, storyEventIndex) => {
        events.push({
          title: storyEvent.title?.trim() || 'Story Event',
          date: storyEvent.date?.trim() || '',
          description: storyEvent.description?.trim() || '',
          chapterTitle: chapter.title,
          chapterIndex,
          storyEventIndex,
        });
      });
    });

    return events;
  }

  selectChapter(event: StorylineTimelineEvent): void {
    this.chapterSelected.emit(event.chapterIndex);
  }
}
