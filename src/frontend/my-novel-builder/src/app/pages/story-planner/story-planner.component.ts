import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterModule } from '@angular/router';
import {
  CdkDragDrop,
  DragDropModule,
  moveItemInArray,
  transferArrayItem,
} from '@angular/cdk/drag-drop';
import { NovelDto } from '../../types/dtos/novel/novel.dto';
import { NovelService } from '../../services/novel.service';
import { Prose, Section } from '../../types/dtos/novel/prose';
import { ButtonModule } from 'primeng/button';
import { ToastrService } from 'ngx-toastr';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';

@Component({
  selector: 'app-story-planner',
  standalone: true,
  imports: [RouterModule, ButtonModule, DragDropModule, ConfirmDialogModule],
  providers: [ConfirmationService],
  templateUrl: './story-planner.component.html',
  styleUrl: './story-planner.component.scss',
})
export class StoryPlannerComponent {
  private route = inject(ActivatedRoute);
  private confirmationService = inject(ConfirmationService);
  readonly novelService: NovelService = inject(NovelService);
  readonly toastrService: ToastrService = inject(ToastrService);

  novel: NovelDto | null = null;
  prose: Prose | null = null;
  novelId!: string;
  private readonly missingSummaryPlaceholder = '[Missing summary]';
  private readonly chapterTitlePlaceholder = '[Untitled chapter]';
  private readonly emptySectionPreviewPlaceholder = 'No summary or text yet.';
  private selectedSections = new Set<Section>();
  private saveToastId: number | undefined;

  ngOnInit(): void {
    this.novelId = this.route.snapshot.paramMap.get('id')!;
    this.getNovel();
    this.getProse();
  }

  getNovel(): void {
    this.novelService.getNovel(this.novelId).subscribe((novel) => {
      this.novel = novel;
    });
  }

  getProse(): void {
    this.novelService.getNovelProse(this.novelId).subscribe((prose) => {
      this.prose = prose;
      this.selectedSections.clear();
    });
  }

  get sectionDropListIds(): string[] {
    return (this.prose?.chapters || []).map((_, chapterIndex) =>
      this.getSectionDropListId(chapterIndex),
    );
  }

  getSectionDropListId(chapterIndex: number): string {
    return `chapter-sections-${chapterIndex}`;
  }

  onChapterDrop(event: CdkDragDrop<Prose['chapters']>): void {
    if (this.prose === null || event.previousIndex === event.currentIndex) {
      return;
    }

    moveItemInArray(this.prose.chapters, event.previousIndex, event.currentIndex);
    this.saveProse();
  }

  onSectionDrop(event: CdkDragDrop<Section[]>): void {
    if (this.prose === null) {
      return;
    }

    if (
      event.previousContainer === event.container &&
      event.previousIndex === event.currentIndex
    ) {
      return;
    }

    const sourceSections = event.previousContainer.data;
    const targetSections = event.container.data;
    const draggedSection = sourceSections[event.previousIndex];
    const selectedInSource = this.getSelectedSectionsFromChapter(sourceSections);
    const shouldMoveSelectedGroup =
      event.previousContainer !== event.container &&
      draggedSection !== undefined &&
      this.selectedSections.has(draggedSection) &&
      selectedInSource.length > 1;

    if (shouldMoveSelectedGroup) {
      this.transferSelectedSections(
        sourceSections,
        targetSections,
        selectedInSource,
        event.currentIndex,
      );
      selectedInSource.forEach((section) => this.selectedSections.delete(section));
      this.saveProse();
      return;
    }

    if (event.previousContainer === event.container) {
      moveItemInArray(event.container.data, event.previousIndex, event.currentIndex);
    } else {
      transferArrayItem(
        sourceSections,
        targetSections,
        event.previousIndex,
        event.currentIndex,
      );

      if (draggedSection !== undefined) {
        this.selectedSections.delete(draggedSection);
      }
    }

    this.saveProse();
  }

  addChapter(): void {
    if (this.prose === null) {
      return;
    }

    this.prose.chapters = this.prose.chapters.concat({
      title: `Chapter ${this.prose.chapters.length + 1}`,
      sections: [],
      storyEvents: [],
    });
    this.saveProse();
  }

  addSection(chapterIndex: number): void {
    if (this.prose === null) {
      return;
    }

    this.prose.chapters[chapterIndex].sections = this.prose.chapters[
      chapterIndex
    ].sections.concat({
      summary: this.missingSummaryPlaceholder,
      text: '',
      images: [],
      recordOverrides: [],
    });
    this.saveProse();
  }

  removeSection(chapterIndex: number, sectionIndex: number): void {
    if (this.prose === null) {
      return;
    }

    this.confirmationService.confirm({
      message:
        'Are you sure you want to delete this section? This action cannot be undone.',
      header: 'Confirm Section Deletion',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        const section = this.prose!.chapters[chapterIndex].sections[sectionIndex];
        if (section !== undefined) {
          this.selectedSections.delete(section);
        }

        this.prose!.chapters[chapterIndex].sections = this.prose!.chapters[
          chapterIndex
        ].sections.filter((_, index) => index !== sectionIndex);
        this.saveProse();
      },
    });
  }

  removeChapter(chapterIndex: number): void {
    if (this.prose === null) {
      return;
    }

    const sectionCount = this.prose.chapters[chapterIndex].sections.length;
    const hasSections = sectionCount > 0;

    this.confirmationService.confirm({
      message: hasSections
        ? `WARNING: This chapter contains ${sectionCount} section${sectionCount === 1 ? '' : 's'}. Deleting it will permanently remove all of them.`
        : 'Are you sure you want to delete this empty chapter? This action cannot be undone.',
      header: hasSections
        ? 'Delete Chapter With Existing Sections'
        : 'Confirm Chapter Deletion',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.prose!.chapters[chapterIndex].sections.forEach((section) =>
          this.selectedSections.delete(section),
        );

        this.prose!.chapters = this.prose!.chapters.filter(
          (_, index) => index !== chapterIndex,
        );
        this.saveProse();
      },
    });
  }

  isSectionSelected(section: Section): boolean {
    return this.selectedSections.has(section);
  }

  toggleSectionSelection(section: Section, event: Event): void {
    const input = event.target as HTMLInputElement;

    if (input.checked) {
      this.selectedSections.add(section);
      return;
    }

    this.selectedSections.delete(section);
  }

  getSectionPreview(section: Section): string {
    const summary = section.summary?.trim();
    if (
      summary !== undefined &&
      summary !== '' &&
      summary !== this.missingSummaryPlaceholder
    ) {
      return summary;
    }

    const textPreview = this.stripHtml(section.text);
    if (textPreview !== '') {
      return textPreview;
    }

    return this.emptySectionPreviewPlaceholder;
  }

  updateChapterTitle(chapterIndex: number, event: Event): void {
    if (this.prose === null) {
      return;
    }

    const elem = event.target as HTMLElement;
    const updatedTitle = elem.innerText.trim();

    if (updatedTitle === '') {
      this.prose.chapters[chapterIndex].title = this.chapterTitlePlaceholder;
      elem.innerText = this.chapterTitlePlaceholder;
      this.saveProse();
      return;
    }

    if (this.prose.chapters[chapterIndex].title === updatedTitle) {
      return;
    }

    this.prose.chapters[chapterIndex].title = updatedTitle;
    this.saveProse();
  }

  preventReturnKey(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
    }
  }

  private getSelectedSectionsFromChapter(chapterSections: Section[]): Section[] {
    return chapterSections.filter((section) => this.selectedSections.has(section));
  }

  private transferSelectedSections(
    sourceSections: Section[],
    targetSections: Section[],
    selectedSections: Section[],
    insertionIndex: number,
  ): void {
    const selectedSectionSet = new Set(selectedSections);
    for (let index = sourceSections.length - 1; index >= 0; index -= 1) {
      if (selectedSectionSet.has(sourceSections[index])) {
        sourceSections.splice(index, 1);
      }
    }

    const normalizedInsertionIndex = Math.max(
      0,
      Math.min(insertionIndex, targetSections.length),
    );
    targetSections.splice(normalizedInsertionIndex, 0, ...selectedSections);
  }

  private stripHtml(value: string): string {
    const parser = new DOMParser();
    const doc = parser.parseFromString(value, 'text/html');
    return (doc.body.textContent || '').replace(/\s+/g, ' ').trim();
  }

  private saveProse(): void {
    if (this.prose === null) {
      return;
    }

    this.novelService.updateNovelProse(this.novelId, this.prose).subscribe({
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
}
