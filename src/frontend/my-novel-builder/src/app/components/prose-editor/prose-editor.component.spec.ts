import { TestBed } from '@angular/core/testing';
import { ConfirmationService } from 'primeng/api';
import type { Confirmation } from 'primeng/api';
import { DialogService } from 'primeng/dynamicdialog';
import { of } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import type { Blur } from 'ngx-quill';
import { GenerateAudioService } from '../../services/generate-audio.service';
import { GenerateTextService } from '../../services/generate-text.service';
import { IntegrationsService } from '../../services/integrations.service';
import { LocalStorageService } from '../../services/local-storage.service';
import { NovelService } from '../../services/novel.service';
import type { Prose, Section } from '../../types/dtos/novel/prose';
import { ProseEditorComponent } from './prose-editor.component';

describe('ProseEditorComponent mutations', () => {
  let component: ProseEditorComponent;
  let confirmationService: jasmine.SpyObj<ConfirmationService>;
  let toastr: jasmine.SpyObj<ToastrService>;

  const createSection = (
    summary = 'Summary',
    text = '<p>Text</p>',
  ): Section => ({
    summary,
    text,
    images: [],
    recordOverrides: [],
  });

  const createProse = (): Prose => ({
    chapters: [
      {
        title: 'Chapter 1',
        sections: [createSection()],
        storyEvents: [],
      },
    ],
  });

  beforeEach(() => {
    confirmationService = jasmine.createSpyObj<ConfirmationService>(
      'ConfirmationService',
      ['confirm'],
    );
    toastr = jasmine.createSpyObj<ToastrService>('ToastrService', ['error']);

    TestBed.configureTestingModule({
      providers: [
        { provide: ConfirmationService, useValue: confirmationService },
        { provide: DialogService, useValue: {} },
        { provide: ToastrService, useValue: toastr },
        { provide: GenerateTextService, useValue: {} },
        { provide: GenerateAudioService, useValue: {} },
        { provide: NovelService, useValue: {} },
        { provide: LocalStorageService, useValue: {} },
        {
          provide: IntegrationsService,
          useValue: {
            getIntegrationsConfig: () => of({ ttsEnableImmersive: false }),
          },
        },
      ],
    });

    component = TestBed.runInInjectionContext(() => new ProseEditorComponent());
    component.prose = createProse();
  });

  it('adds a chapter with initialized collections and emits the prose', () => {
    const originalChapters = component.prose.chapters;
    const emit = spyOn(component.proseChange, 'emit');

    component.addChapter();

    expect(component.prose.chapters).not.toBe(originalChapters);
    expect(component.prose.chapters[1]).toEqual({
      title: 'Chapter 2',
      sections: [],
      storyEvents: [],
    });
    expect(emit).toHaveBeenCalledOnceWith(component.prose);
  });

  it('removes an empty chapter and emits the prose', () => {
    component.prose.chapters.push({
      title: 'Empty chapter',
      sections: [],
      storyEvents: [],
    });
    const emit = spyOn(component.proseChange, 'emit');

    component.removeChapter(1);

    expect(component.prose.chapters.map((chapter) => chapter.title)).toEqual([
      'Chapter 1',
    ]);
    expect(emit).toHaveBeenCalledOnceWith(component.prose);
  });

  it('does not remove a non-empty chapter', () => {
    const emit = spyOn(component.proseChange, 'emit');

    component.removeChapter(0);

    expect(component.prose.chapters).toHaveSize(1);
    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Cannot remove a chapter that is not empty. Please remove all sections first.',
    );
    expect(emit).not.toHaveBeenCalled();
  });

  it('adds an initialized section and emits the prose', () => {
    const originalSections = component.prose.chapters[0].sections;
    const emit = spyOn(component.proseChange, 'emit');

    component.addSection(0);

    expect(component.prose.chapters[0].sections).not.toBe(originalSections);
    expect(component.prose.chapters[0].sections[1]).toEqual({
      summary: '[Missing summary]',
      text: '',
      images: [],
      recordOverrides: [],
    });
    expect(emit).toHaveBeenCalledOnceWith(component.prose);
  });

  it('removes a section only after confirmation is accepted', () => {
    component.prose.chapters[0].sections.push(createSection('Second'));
    const emit = spyOn(component.proseChange, 'emit');

    component.removeSection(0, 0);

    expect(component.prose.chapters[0].sections).toHaveSize(2);
    expect(emit).not.toHaveBeenCalled();

    const confirmation = confirmationService.confirm.calls.mostRecent()
      .args[0] as Confirmation;
    confirmation.accept?.();

    expect(component.prose.chapters[0].sections).toEqual([
      jasmine.objectContaining({ summary: 'Second' }),
    ]);
    expect(emit).toHaveBeenCalledOnceWith(component.prose);
  });

  it('updates non-empty chapter titles and restores empty ones', () => {
    const emit = spyOn(component.proseChange, 'emit');
    const title = document.createElement('div');
    title.innerText = 'Renamed chapter';

    component.updateChapterTitle(0, { target: title } as unknown as Event);

    expect(component.prose.chapters[0].title).toBe('Renamed chapter');
    expect(emit).toHaveBeenCalledOnceWith(component.prose);

    title.innerText = '   ';
    component.updateChapterTitle(0, { target: title } as unknown as Event);

    expect(title.innerText).toBe('Renamed chapter');
    expect(component.prose.chapters[0].title).toBe('Renamed chapter');
    expect(emit).toHaveBeenCalledTimes(1);
  });

  it('updates section text from the editor and emits the prose', () => {
    const section = component.prose.chapters[0].sections[0];
    const emit = spyOn(component.proseChange, 'emit');
    const blurEvent = {
      editor: {
        getSemanticHTML: () => '<p>Updated text</p>',
      },
    } as unknown as Blur;

    component.updateSectionText(section, blurEvent);

    expect(section.text).toBe('<p>Updated text</p>');
    expect(emit).toHaveBeenCalledOnceWith(component.prose);
  });

  it('uses the fallback for an empty section summary and emits the prose', () => {
    const section = component.prose.chapters[0].sections[0];
    const emit = spyOn(component.proseChange, 'emit');
    const summary = document.createElement('div');
    summary.innerText = '   ';

    component.updateSectionSummary(
      section,
      { target: summary } as unknown as Event,
    );

    expect(summary.innerText).toBe('[Missing summary]');
    expect(section.summary).toBe('[Missing summary]');
    expect(emit).toHaveBeenCalledOnceWith(component.prose);
  });
});
