import { TestBed } from '@angular/core/testing';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import {
  StoryEventDialogComponent,
  StoryEventDialogData,
} from './story-event-dialog.component';

describe('StoryEventDialogComponent workflow', () => {
  let component: StoryEventDialogComponent;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let config: { data?: StoryEventDialogData };

  const createComponent = (): StoryEventDialogComponent =>
    TestBed.runInInjectionContext(() => new StoryEventDialogComponent());

  beforeEach(() => {
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>('DynamicDialogRef', [
      'close',
    ]);
    config = {
      data: {
        mode: 'create',
        chapters: [
          { label: 'Chapter One', value: 0 },
          { label: 'Chapter Two', value: 1 },
        ],
        selectedChapterIndex: 1,
      },
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: DynamicDialogConfig, useValue: config },
        { provide: DynamicDialogRef, useValue: dialogRef },
      ],
    });

    component = createComponent();
  });

  it('initializes a create form with the selected chapter', () => {
    component.ngOnInit();

    expect(component.data).toBe(config.data!);
    expect(component.formGroup.getRawValue()).toEqual({
      chapterIndex: 1,
      date: '',
      title: '',
      description: '',
    });
    expect(component.formGroup.valid).toBeTrue();
  });

  it('initializes edit fields and normalizes missing optional values', () => {
    config.data = {
      ...config.data!,
      mode: 'edit',
      storyEvent: {
        date: undefined as unknown as string,
        title: 'Arrival',
        description: undefined as unknown as string,
      },
    };
    component = createComponent();

    component.ngOnInit();

    expect(component.formGroup.getRawValue()).toEqual({
      chapterIndex: 1,
      date: '',
      title: 'Arrival',
      description: '',
    });
  });

  it('uses safe defaults when dialog data is absent', () => {
    config.data = undefined;
    component = createComponent();

    component.ngOnInit();

    expect(component.data).toEqual({
      mode: 'create',
      chapters: [],
      selectedChapterIndex: 0,
    });
    component.submit();
    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('enforces field length limits', () => {
    component.ngOnInit();
    component.formGroup.patchValue({
      date: 'd'.repeat(201),
      title: 't'.repeat(201),
      description: 'x'.repeat(10_001),
    });

    expect(component.formGroup.controls.date.hasError('maxlength')).toBeTrue();
    expect(component.formGroup.controls.title.hasError('maxlength')).toBeTrue();
    expect(
      component.formGroup.controls.description.hasError('maxlength'),
    ).toBeTrue();
  });

  it('does not submit an invalid form', () => {
    component.ngOnInit();
    component.formGroup.controls.chapterIndex.setValue(null);

    component.submit();

    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('does not submit a chapter index outside the supplied choices', () => {
    component.ngOnInit();
    component.formGroup.controls.chapterIndex.setValue(99);

    component.submit();

    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('trims event fields and closes with the selected chapter', () => {
    component.ngOnInit();
    component.formGroup.setValue({
      chapterIndex: 0,
      date: '  Two days later  ',
      title: '  The Return  ',
      description: '  The party comes home.  ',
    });

    component.submit();

    expect(dialogRef.close).toHaveBeenCalledOnceWith({
      chapterIndex: 0,
      storyEvent: {
        date: 'Two days later',
        title: 'The Return',
        description: 'The party comes home.',
      },
    });
  });

  it('normalizes nullable event values to empty strings', () => {
    component.ngOnInit();
    component.formGroup.patchValue({
      date: null,
      title: null,
      description: null,
    });

    component.submit();

    expect(dialogRef.close).toHaveBeenCalledOnceWith({
      chapterIndex: 1,
      storyEvent: {
        date: '',
        title: '',
        description: '',
      },
    });
  });
});
