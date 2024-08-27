import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import {
  Prose,
  TextSectionItem,
  SectionItem,
  SectionItemType,
  ImageSectionItem,
  Section,
} from '../../types/dtos/novel/prose';
import { CommonModule } from '@angular/common';
import { Blur, QuillModule } from 'ngx-quill';
import { environment } from '../../../environment';
import { FormsModule } from '@angular/forms';
import { ToastrModule, ToastrService } from 'ngx-toastr';

@Component({
  selector: 'app-prose-editor',
  standalone: true,
  imports: [CommonModule, FormsModule, QuillModule, ToastrModule],
  templateUrl: './prose-editor.component.html',
  styleUrl: './prose-editor.component.scss',
})
export class ProseEditorComponent {
  @Input() novelId!: string;
  @Input() prose!: Prose;
  @Output() proseChange: EventEmitter<Prose> = new EventEmitter<Prose>();
  toastr: ToastrService = inject(ToastrService);

  isTextSectionItem(item: SectionItem): item is TextSectionItem {
    return item.$type === SectionItemType.Text;
  }

  isImageSectionItem(item: SectionItem): item is ImageSectionItem {
    return item.$type === SectionItemType.Image;
  }

  getImageUrl(imageId: string): string {
    // TODO: This should come directly from the API in ImageSectionItem
    // instead of being built in the client.
    return `${environment.api.staticFilesUrl}/novels/${this.novelId}/images/${imageId}`;
  }

  addChapter() {
    this.prose.chapters = this.prose.chapters.concat({
      title: `Chapter ${this.prose.chapters.length + 1}`,
      sections: [],
    });
    this.saveProse();
  }

  removeChapter(chapterIndex: number) {
    // To remove a chapter, it must be empty to avoid user data loss
    if (this.prose.chapters[chapterIndex].sections.length > 0) {
      this.toastr.error(
        'Cannot remove a chapter that is not empty. Please remove all sections first.'
      );
      return;
    }

    this.prose.chapters = this.prose.chapters.filter(
      (_, index) => index !== chapterIndex
    );
    this.saveProse();
  }

  addSection(chapterIndex: number) {
    // For now:
    // - a section is always added at the end of the chapter
    // - a section is only made up of a single text item, since the
    //   WYSIWYG editor can handle images on its own without the need
    //   for a separate section type
    this.prose.chapters[chapterIndex].sections = this.prose.chapters[
      chapterIndex
    ].sections.concat({
      summary: '[Missing summary]',
      items: [
        <TextSectionItem>{
          $type: SectionItemType.Text,
          text: '',
        },
      ],
    });
    this.saveProse();
  }

  removeSection(chapterIndex: number, sectionIndex: number) {
    // Ask for confirmation before removing a section
    // TODO: This should be a material modal dialog instead of a browser dialog
    if (
      !confirm(
        'Are you sure you want to remove this section? This action cannot be undone.'
      )
    ) {
      return;
    }

    this.prose.chapters[chapterIndex].sections = this.prose.chapters[
      chapterIndex
    ].sections.filter((_, index) => index !== sectionIndex);
    this.saveProse();
  }

  addTextSectionItem(chapterIndex: number, sectionIndex: number) {
    this.prose.chapters[chapterIndex].sections[sectionIndex].items =
      this.prose.chapters[chapterIndex].sections[sectionIndex].items.concat(<
        TextSectionItem
      >{
        $type: SectionItemType.Text,
        text: '',
      });
    this.saveProse();
  }

  updateChapterTitle(chapterIndex: number, event: Event) {
    const elem = event.target as HTMLInputElement;

    // If the user tries to remove the title, restore it
    if (elem.innerText.trim() === '') {
      elem.innerText = this.prose.chapters[chapterIndex].title;
      return;
    }

    this.prose.chapters[chapterIndex].title = elem.innerText;
    this.saveProse();
  }

  updateTextSectionItem(item: TextSectionItem, event: Blur) {
    console.log(event);
    item.text = event.editor.getSemanticHTML();
    this.saveProse();
  }

  updateSectionSummary(section: Section, event: Event) {
    const elem = event.target as HTMLInputElement;

    // HACK: This is an ugly workaround to avoid empty summaries, otherwise
    // the user would not be able to click on the summary to edit it.
    if (elem.innerText.trim() === '') {
      elem.innerText = '[Missing summary]';
    }

    section.summary = elem.innerText;
    this.saveProse();
  }

  saveProse() {
    // TODO: This should be debounced to avoid sending too many requests
    // TODO: Don't send the entire prose, only the changed parts
    this.proseChange.emit(this.prose);
  }

  preventReturnKey(event: KeyboardEvent) {
    if (event.key === 'Enter') {
      event.preventDefault();
    }
  }
}
