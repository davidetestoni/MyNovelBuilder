import { TestBed } from '@angular/core/testing';
import { ToastrService } from 'ngx-toastr';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { of, Subject, throwError } from 'rxjs';
import { NovelService } from '../../services/novel.service';
import { ImportMarkdownDialogComponent } from './import-markdown-dialog.component';

describe('ImportMarkdownDialogComponent', () => {
  let novelService: jasmine.SpyObj<NovelService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let toastr: jasmine.SpyObj<ToastrService>;

  const createComponent = (
    data: { novelId: string } = { novelId: 'existing-novel' },
  ): ImportMarkdownDialogComponent => {
    TestBed.configureTestingModule({
      providers: [
        { provide: NovelService, useValue: novelService },
        { provide: DynamicDialogRef, useValue: dialogRef },
        { provide: DynamicDialogConfig, useValue: { data } },
        { provide: ToastrService, useValue: toastr },
      ],
    });

    return TestBed.runInInjectionContext(
      () => new ImportMarkdownDialogComponent(),
    );
  };

  const fileEvent = (file: File): Event => {
    const input = document.createElement('input');
    Object.defineProperty(input, 'files', { value: [file] });
    return { target: input } as unknown as Event;
  };

  beforeEach(() => {
    novelService = jasmine.createSpyObj<NovelService>('NovelService', [
      'replaceNovelProseFromMarkdown',
    ]);
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>('DynamicDialogRef', [
      'close',
    ]);
    toastr = jasmine.createSpyObj<ToastrService>('ToastrService', [
      'success',
      'error',
    ]);
    novelService.replaceNovelProseFromMarkdown.and.returnValue(of(undefined));
  });

  it('replaces prose with the selected Markdown file', () => {
    const component = createComponent({ novelId: 'existing-novel' });
    const file = new File(['# Source\n\n## Chapter'], 'novel.markdown');

    component.onFileSelected(fileEvent(file));
    component.importMarkdown();

    expect(
      novelService.replaceNovelProseFromMarkdown,
    ).toHaveBeenCalledOnceWith('existing-novel', file);
    expect(toastr.success).toHaveBeenCalledOnceWith(
      'Novel prose replaced successfully.',
    );
    expect(dialogRef.close).toHaveBeenCalledOnceWith({
      novelId: 'existing-novel',
    });
  });

  it('imports pasted Markdown as a generated Markdown file', async () => {
    const component = createComponent({ novelId: 'existing-novel' });
    const markdown = '# Pasted novel\n\n## Chapter 1\nPasted prose.';

    component.onPastedMarkdownChange(markdown);
    component.importMarkdown();

    const [novelId, file] =
      novelService.replaceNovelProseFromMarkdown.calls.mostRecent().args;
    expect(novelId).toBe('existing-novel');
    expect(file.name).toBe('pasted-markdown.md');
    expect(file.type).toBe('text/markdown');
    expect(await file.text()).toBe(markdown);
  });

  it('uses whichever Markdown source was selected most recently', () => {
    const component = createComponent();
    const file = new File(['# File novel'], 'novel.md');

    component.onPastedMarkdownChange('# Pasted novel');
    expect(component.selectedFile).toBeNull();
    expect(component.hasMarkdownSource).toBeTrue();

    component.onFileSelected(fileEvent(file));
    expect(component.selectedFile).toBe(file);
    expect(component.pastedMarkdown).toBe('');

    component.onPastedMarkdownChange('# New pasted novel');
    expect(component.selectedFile).toBeNull();
    expect(component.pastedMarkdown).toBe('# New pasted novel');
  });

  it('rejects a selected file with an unsupported extension', () => {
    const component = createComponent();

    component.onFileSelected(fileEvent(new File(['text'], 'novel.txt')));
    component.importMarkdown();

    expect(component.selectedFile).toBeNull();
    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Choose a Markdown file ending in .md or .markdown.',
    );
    expect(
      novelService.replaceNovelProseFromMarkdown,
    ).not.toHaveBeenCalled();
  });

  it('prevents duplicate imports while the request is pending', () => {
    const response = new Subject<void>();
    const component = createComponent();
    const file = new File(['# Novel'], 'novel.md');
    novelService.replaceNovelProseFromMarkdown.and.returnValue(response);
    component.selectedFile = file;

    component.importMarkdown();
    component.importMarkdown();

    expect(component.isImporting).toBeTrue();
    expect(
      novelService.replaceNovelProseFromMarkdown,
    ).toHaveBeenCalledTimes(1);

    response.next();
    response.complete();
    expect(component.isImporting).toBeFalse();
  });

  it('rejects pasted Markdown larger than 5 MB', () => {
    const component = createComponent();
    component.pastedMarkdown = `# Novel\n${'a'.repeat(5 * 1024 * 1024)}`;

    component.importMarkdown();

    expect(toastr.error).toHaveBeenCalledOnceWith(
      'The Markdown content cannot exceed 5 MB.',
    );
    expect(
      novelService.replaceNovelProseFromMarkdown,
    ).not.toHaveBeenCalled();
  });

  it('restores the form and reports a failed import', () => {
    const component = createComponent();
    component.selectedFile = new File(['# Novel'], 'novel.md');
    novelService.replaceNovelProseFromMarkdown.and.returnValue(
      throwError(() => new Error('failed')),
    );

    component.importMarkdown();

    expect(component.isImporting).toBeFalse();
    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Failed to import the Markdown file.',
    );
    expect(dialogRef.close).not.toHaveBeenCalled();
  });
});
