import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NovelDto } from '../../types/dtos/novel/novel.dto';
import { NovelService } from '../../services/novel.service';
import { Prose, StoryEvent } from '../../types/dtos/novel/prose';
import { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import { CompendiumService } from '../../services/compendium.service';
import { FormsModule } from '@angular/forms';
import { CompendiumRecordOverviewDto } from '../../types/dtos/compendium-record/compendium-record-overview.dto';
import { CompendiumRecordDto } from '../../types/dtos/compendium-record/compendium-record.dto';
import { SpacedPipe } from '../../pipes/spaced.pipe';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { CompendiumRecordMediaDto } from '../../types/dtos/compendium-record/compendium-record-media.dto';
import { ProseEditorComponent } from '../../components/prose-editor/prose-editor.component';
import { PromptService } from '../../services/prompt.service';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { InputTextModule } from 'primeng/inputtext';
import { StorylineComponent } from '../../components/storyline/storyline.component';
import { ToastrService } from 'ngx-toastr';
import * as ExifReader from 'exifreader';
import { CompendiumOptionPreviewComponent } from '../../components/compendium-option-preview/compendium-option-preview.component';

@Component({
  selector: 'app-novel-editor',
  standalone: true,
  imports: [
    FormsModule,
    RouterModule,
    SpacedPipe,
    ProseEditorComponent,
    StorylineComponent,
    ButtonModule,
    SelectModule,
    InputTextModule,
    CompendiumOptionPreviewComponent,
  ],
  templateUrl: './novel-editor.component.html',
  styleUrl: './novel-editor.component.scss',
})
export class NovelEditorComponent {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private readonly chapterQueryParamName = 'chapter';
  private readonly sidebarCollapsedStorageKey =
    'novel-editor-left-sidebar-collapsed';
  private chapterSelectionFromQuery: number | null | undefined = undefined;

  compendia: CompendiumDto[] | null = null;
  prompts: PromptDto[] | null = null; // TODO: Send a lighter version of this DTO
  novel: NovelDto | null = null;
  prose = signal<Prose | null>(null);
  chapters = computed(() => {
    const prose = this.prose();
    if (!prose) {
      return [];
    }
    return prose.chapters.map((chapter, index) => ({
      label: chapter.title,
      value: index,
    }));
  });

  readonly novelService: NovelService = inject(NovelService);
  readonly promptService: PromptService = inject(PromptService);
  readonly compendiumService: CompendiumService = inject(CompendiumService);
  readonly toastrService: ToastrService = inject(ToastrService);
  novelId!: string;
  recordsFilter = '';
  selectedCompendium: CompendiumDto | null = null;
  selectedRecordOverview: CompendiumRecordOverviewDto | null = null;
  selectedRecord: CompendiumRecordDto | null = null;
  floatedMedia: CompendiumRecordMediaDto[] = [];
  lastHoveredFloatingMediaId: string | null = null;
  zoomedMedia: CompendiumRecordMediaDto | null = null;
  zoomedMediaPrompt: string | null = null;
  isZoomedMediaPromptLoading = false;
  selectedChapterIndex: number | null = null;
  isStoryTimelineOpen = signal(false);
  isLeftSidebarCollapsed = signal(this.readLeftSidebarCollapsedState());
  private saveToastId: number | undefined;
  private zoomedPromptRequestId = 0;

  compendiumRecordTypes: CompendiumRecordType[] = [
    CompendiumRecordType.Character,
    CompendiumRecordType.Place,
    CompendiumRecordType.Object,
    CompendiumRecordType.Event,
    CompendiumRecordType.Concept,
    CompendiumRecordType.Other,
  ];

  CompendiumRecordType = CompendiumRecordType;

  ngOnInit(): void {
    this.novelId = this.route.snapshot.paramMap.get('id')!;
    this.chapterSelectionFromQuery = this.readChapterSelectionFromQuery();
    this.getNovel();
    this.getProse();
    this.getPrompts();
    this.restoreFloatedMedia();
  }

  getNovel(): void {
    this.novelService.getNovel(this.novelId).subscribe((novel) => {
      this.novel = novel;
      this.getCompendia();
    });
  }

  getProse(): void {
    this.novelService.getNovelProse(this.novelId).subscribe((prose) => {
      this.prose.set(prose);

      if (prose.chapters.length === 0) {
        this.selectedChapterIndex = null;
        return;
      }

      if (this.chapterSelectionFromQuery === null) {
        this.selectedChapterIndex = null;
        return;
      }

      if (
        this.chapterSelectionFromQuery !== undefined &&
        this.chapterSelectionFromQuery < prose.chapters.length
      ) {
        this.selectedChapterIndex = this.chapterSelectionFromQuery;
        return;
      }

      // No chapter query param -> default to first chapter.
      this.selectedChapterIndex = 0;
    });
  }

  async goToProse(): Promise<void> {
    const proseUrl = `/novel/${this.novelId}`;
    const currentPath =
      '/' +
      this.router
        .parseUrl(this.router.url)
        .root.children['primary']?.segments.map((segment) => segment.path)
        .join('/');

    if (currentPath === proseUrl) {
      await this.router.navigateByUrl('/novels', { skipLocationChange: true });
    }

    await this.router.navigate(['/novel', this.novelId]);
  }

  getPrompts(): void {
    this.promptService.getPrompts().subscribe((prompts) => {
      this.prompts = prompts;
    });
  }

  getCompendia(): void {
    // TODO: Only get the novel's compendia, not all compendia
    this.compendiumService.getCompendia().subscribe((compendia) => {
      this.compendia = compendia.filter((compendium) =>
        this.novel?.compendiumIds.includes(compendium.id),
      );
    });
  }

  restoreFloatedMedia(): void {
    this.floatedMedia = this.novelService.getFloatedMediaForNovel(this.novelId);
  }

  getCompendiumRecordsByType(
    type: CompendiumRecordType,
  ): CompendiumRecordOverviewDto[] {
    if (this.compendia === null) {
      return [];
    }

    if (this.selectedCompendium === null) {
      const records = this.compendia
        .map((compendium) => compendium.records)
        .flat()
        .filter(
          (record) =>
            record.type === type &&
            record.name
              .toLowerCase()
              .includes(this.recordsFilter.toLowerCase()),
        );

      records.sort((a, b) => a.name.localeCompare(b.name));

      return records;
    }

    return this.selectedCompendium.records.filter(
      (record) =>
        record.type === type &&
        record.name.toLowerCase().includes(this.recordsFilter.toLowerCase()),
    );
  }

  selectRecord(record: CompendiumRecordOverviewDto): void {
    // If the record is already selected, deselect it
    if (this.selectedRecordOverview === record) {
      this.selectedRecordOverview = null;
      this.selectedRecord = null;
      return;
    }

    this.selectedRecordOverview = record;
    this.compendiumService.getRecord(record.id).subscribe((record) => {
      this.selectedRecord = record;
    });
  }

  isFloatedMedia(media: CompendiumRecordMediaDto): boolean {
    return this.floatedMedia.some(
      (floatedMedia) => floatedMedia.id === media.id,
    );
  }

  floatMedia(media: CompendiumRecordMediaDto): void {
    // If the media is already floated, unfloat it (use id instead of object reference)
    if (this.isFloatedMedia(media)) {
      this.floatedMedia = this.floatedMedia.filter(
        (floatedMedia) => floatedMedia.id !== media.id,
      );
      this.novelService.setFloatedMediaForNovel(
        this.novelId,
        this.floatedMedia,
      );
      return;
    }

    this.floatedMedia = [...this.floatedMedia, media];
    this.novelService.setFloatedMediaForNovel(this.novelId, this.floatedMedia);
  }

  zoomMedia(media: CompendiumRecordMediaDto): void {
    this.zoomedMedia = media;
    this.loadZoomedMediaPrompt(media);
  }

  unzoomMedia(): void {
    this.zoomedMedia = null;
    this.zoomedMediaPrompt = null;
    this.isZoomedMediaPromptLoading = false;
  }

  updateProse(prose: Prose) {
    // This will trigger the computed chapters to update
    this.prose.set({ ...prose });
    this.novelService.updateNovelProse(this.novelId, prose).subscribe({
      next: () => this.showSaveToast(),
    });
  }

  private showSaveToast(): void {
    if (this.saveToastId !== undefined) {
      this.toastrService.clear(this.saveToastId);
    }

    const toast = this.toastrService.success('', '', {
      toastClass: 'ngx-toastr subtle-save-toast',
      positionClass: 'toast-bottom-right',
      closeButton: false,
      tapToDismiss: true,
      progressBar: false,
      timeOut: 1000,
      extendedTimeOut: 0,
    });

    this.saveToastId = toast.toastId;
  }

  onChapterSelectionChange(chapterIndex: number | null): void {
    const chapterQueryValue = chapterIndex === null ? 'all' : chapterIndex;
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        [this.chapterQueryParamName]: chapterQueryValue,
      },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  private readChapterSelectionFromQuery(): number | null | undefined {
    const chapterQueryValue = this.route.snapshot.queryParamMap.get(
      this.chapterQueryParamName,
    );

    if (chapterQueryValue === null) {
      return undefined;
    }

    if (chapterQueryValue === 'all') {
      return null;
    }

    const parsedChapterIndex = Number.parseInt(chapterQueryValue, 10);
    if (!Number.isNaN(parsedChapterIndex) && parsedChapterIndex >= 0) {
      return parsedChapterIndex;
    }

    return undefined;
  }

  onProseImageClicked(imageUrl: string): void {
    // TODO: This could be done better
    this.zoomMedia({ id: '', url: imageUrl, isCurrent: false, isVideo: false });
  }

  copyZoomedMediaPrompt(): void {
    if (!this.zoomedMediaPrompt) {
      return;
    }

    navigator.clipboard.writeText(this.zoomedMediaPrompt).then(
      () => this.toastrService.success('Prompt copied to clipboard'),
      () => this.toastrService.error('Failed to copy prompt'),
    );
  }

  toggleStoryTimeline(): void {
    this.isStoryTimelineOpen.update((isOpen) => !isOpen);
  }

  toggleLeftSidebar(): void {
    this.isLeftSidebarCollapsed.update((isCollapsed) => {
      const nextState = !isCollapsed;
      this.persistLeftSidebarCollapsedState(nextState);
      return nextState;
    });
  }

  removeStoryEvent(event: {
    chapterIndex: number;
    storyEventIndex: number;
  }): void {
    const prose = this.prose();
    if (!prose) {
      return;
    }

    const chapter = prose.chapters[event.chapterIndex];
    if (!chapter?.storyEvents?.[event.storyEventIndex]) {
      return;
    }

    const updatedChapters = prose.chapters.map(
      (currentChapter, chapterIndex) =>
        chapterIndex === event.chapterIndex
          ? {
              ...currentChapter,
              storyEvents: (currentChapter.storyEvents || []).filter(
                (_, storyEventIndex) =>
                  storyEventIndex !== event.storyEventIndex,
              ),
            }
          : currentChapter,
    );

    this.updateProse({ ...prose, chapters: updatedChapters });
  }

  private async loadZoomedMediaPrompt(
    media: CompendiumRecordMediaDto,
  ): Promise<void> {
    if (media.isVideo) {
      this.zoomedMediaPrompt = null;
      this.isZoomedMediaPromptLoading = false;
      return;
    }

    const requestId = ++this.zoomedPromptRequestId;
    this.isZoomedMediaPromptLoading = true;
    this.zoomedMediaPrompt = null;

    try {
      const response = await fetch(media.url);
      if (!response.ok) {
        return;
      }

      const imageBuffer = await response.arrayBuffer();
      const prompt = await this.extractPromptFromImageMetadata(imageBuffer);

      if (requestId === this.zoomedPromptRequestId) {
        this.zoomedMediaPrompt = prompt;
      }
    } catch {
      if (requestId === this.zoomedPromptRequestId) {
        this.zoomedMediaPrompt = null;
      }
    } finally {
      if (requestId === this.zoomedPromptRequestId) {
        this.isZoomedMediaPromptLoading = false;
      }
    }
  }

  private async extractPromptFromImageMetadata(
    imageBuffer: ArrayBuffer,
  ): Promise<string | null> {
    try {
      const tags = (await ExifReader.load(imageBuffer, {
        expanded: true,
        async: true,
      })) as ExifReader.ExpandedTags;

      console.log('Extracted tags from image metadata:', tags.pngText);

      return tags.pngText?.['prompt (en)']?.description || null;
    } catch {
      return null;
    }
  }

  createStoryEvent(event: {
    chapterIndex: number;
    storyEvent: StoryEvent;
  }): void {
    const prose = this.prose();
    if (!prose || !prose.chapters[event.chapterIndex]) {
      return;
    }

    const updatedChapters = prose.chapters.map((chapter, chapterIndex) =>
      chapterIndex === event.chapterIndex
        ? {
            ...chapter,
            storyEvents: [...(chapter.storyEvents || []), event.storyEvent],
          }
        : chapter,
    );

    this.updateProse({ ...prose, chapters: updatedChapters });
  }

  updateStoryEvent(event: {
    chapterIndex: number;
    storyEventIndex: number;
    storyEvent: StoryEvent;
  }): void {
    const prose = this.prose();
    if (!prose) {
      return;
    }

    const chapter = prose.chapters[event.chapterIndex];
    if (!chapter?.storyEvents?.[event.storyEventIndex]) {
      return;
    }

    const updatedChapters = prose.chapters.map(
      (currentChapter, chapterIndex) =>
        chapterIndex === event.chapterIndex
          ? {
              ...currentChapter,
              storyEvents: (currentChapter.storyEvents || []).map(
                (storyEvent, storyEventIndex) =>
                  storyEventIndex === event.storyEventIndex
                    ? event.storyEvent
                    : storyEvent,
              ),
            }
          : currentChapter,
    );

    this.updateProse({ ...prose, chapters: updatedChapters });
  }

  reorderStoryEvents(event: {
    previousChapterIndex: number;
    currentChapterIndex: number;
    previousIndex: number;
    currentIndex: number;
  }): void {
    const prose = this.prose();
    if (!prose) {
      return;
    }

    const previousChapter = prose.chapters[event.previousChapterIndex];
    const currentChapter = prose.chapters[event.currentChapterIndex];
    const previousStoryEvents = [...(previousChapter?.storyEvents || [])];
    const currentStoryEvents =
      event.previousChapterIndex === event.currentChapterIndex
        ? previousStoryEvents
        : [...(currentChapter?.storyEvents || [])];

    if (
      !previousChapter ||
      !currentChapter ||
      event.previousIndex < 0 ||
      event.currentIndex < 0 ||
      event.previousIndex >= previousStoryEvents.length ||
      event.currentIndex > currentStoryEvents.length ||
      (event.previousChapterIndex === event.currentChapterIndex &&
        event.previousIndex === event.currentIndex)
    ) {
      return;
    }

    const [movedStoryEvent] = previousStoryEvents.splice(
      event.previousIndex,
      1,
    );
    if (!movedStoryEvent) {
      return;
    }

    currentStoryEvents.splice(event.currentIndex, 0, movedStoryEvent);

    const updatedChapters = prose.chapters.map(
      (currentChapter, chapterIndex) =>
        chapterIndex === event.previousChapterIndex &&
        event.previousChapterIndex === event.currentChapterIndex
          ? {
              ...currentChapter,
              storyEvents: currentStoryEvents,
            }
          : chapterIndex === event.previousChapterIndex
            ? {
                ...currentChapter,
                storyEvents: previousStoryEvents,
              }
            : chapterIndex === event.currentChapterIndex
              ? {
                  ...currentChapter,
                  storyEvents: currentStoryEvents,
                }
              : currentChapter,
    );

    this.updateProse({ ...prose, chapters: updatedChapters });
  }

  private readLeftSidebarCollapsedState(): boolean {
    if (typeof localStorage === 'undefined') {
      return false;
    }

    return (
      localStorage.getItem(this.sidebarCollapsedStorageKey) === 'true'
    );
  }

  private persistLeftSidebarCollapsedState(isCollapsed: boolean): void {
    if (typeof localStorage === 'undefined') {
      return;
    }

    localStorage.setItem(
      this.sidebarCollapsedStorageKey,
      String(isCollapsed),
    );
  }

  addGeneratedStoryEvents(
    chapters: { chapterIndex: number; storyEvents: StoryEvent[] }[],
  ): void {
    const prose = this.prose();
    if (!prose || chapters.length === 0) {
      return;
    }

    const updates = new Map<number, StoryEvent[]>();
    for (const chapter of chapters) {
      if (
        Number.isInteger(chapter.chapterIndex) &&
        prose.chapters[chapter.chapterIndex]
      ) {
        updates.set(chapter.chapterIndex, chapter.storyEvents);
      }
    }

    if (updates.size === 0) {
      return;
    }

    const updatedChapters = prose.chapters.map((chapter, chapterIndex) => {
      const generatedEvents = updates.get(chapterIndex);
      if (!generatedEvents || generatedEvents.length === 0) {
        return chapter;
      }

      return {
        ...chapter,
        storyEvents: [...(chapter.storyEvents || []), ...generatedEvents],
      };
    });

    this.updateProse({ ...prose, chapters: updatedChapters });
  }
}
