import { TestBed } from '@angular/core/testing';
import { ToastrService } from 'ngx-toastr';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { of, Subject, throwError } from 'rxjs';
import { CompendiumService } from '../../services/compendium.service';
import { CompendiumRecordDto } from '../../types/dtos/compendium-record/compendium-record.dto';
import { CompendiumRecordOverviewDto } from '../../types/dtos/compendium-record/compendium-record-overview.dto';
import { Prose, RecordOverride } from '../../types/dtos/novel/prose';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import {
  RecordOverridesEditorComponent,
  RecordOverridesEditorComponentData,
} from './record-overrides-editor.component';

describe('RecordOverridesEditorComponent workflows', () => {
  let component: RecordOverridesEditorComponent;
  let compendiumService: jasmine.SpyObj<CompendiumService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let toastr: jasmine.SpyObj<ToastrService>;
  let config: { data: RecordOverridesEditorComponentData };

  const availableRecords: CompendiumRecordOverviewDto[] = [
    {
      id: 'aria',
      name: 'Aria',
      type: CompendiumRecordType.Character,
      imageUrl: '/aria.png',
    },
    {
      id: 'tower',
      name: 'The Tower',
      type: CompendiumRecordType.Place,
      imageUrl: null,
    },
  ];

  const override = (
    description: string,
    keyword = 'appearance',
    compendiumRecordId = 'aria',
  ): RecordOverride => ({
    compendiumRecordId,
    keyword,
    description,
  });

  const prose = (): Prose => ({
    chapters: [
      {
        title: 'Chapter One',
        storyEvents: [],
        sections: [
          {
            summary: '',
            text: '',
            images: [],
            recordOverrides: [override('Blue cloak')],
          },
          {
            summary: '',
            text: '',
            images: [],
            recordOverrides: [override('Red cloak')],
          },
        ],
      },
      {
        title: 'Chapter Two',
        storyEvents: [],
        sections: [
          {
            summary: '',
            text: '',
            images: [],
            recordOverrides: [],
          },
        ],
      },
    ],
  });

  const fullRecord = (
    id: string,
    context = '[appearance]Original cloak[/appearance]\n[goal]Win[/goal]',
  ): CompendiumRecordDto => ({
    id,
    createdAt: '',
    updatedAt: '',
    name: id === 'aria' ? 'Aria' : 'The Tower',
    aliases: '',
    type:
      id === 'aria'
        ? CompendiumRecordType.Character
        : CompendiumRecordType.Place,
    context,
    contextTokenCount: 3,
    media: [],
    compendiumId: 'compendium',
    alwaysIncluded: false,
    characterVoiceAssignments: [],
  });

  const createComponent = (
    recordOverrides: RecordOverride[] = [],
    chapterIndex = 1,
    sectionIndex = 0,
  ): RecordOverridesEditorComponent => {
    config.data = {
      recordOverrides,
      availableRecords,
      prose: prose(),
      chapterIndex,
      sectionIndex,
    };
    return TestBed.runInInjectionContext(
      () => new RecordOverridesEditorComponent(),
    );
  };

  beforeEach(() => {
    compendiumService = jasmine.createSpyObj<CompendiumService>(
      'CompendiumService',
      ['getRecord'],
    );
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>('DynamicDialogRef', [
      'close',
    ]);
    toastr = jasmine.createSpyObj<ToastrService>('ToastrService', ['error']);
    config = {
      data: {
        recordOverrides: [],
        availableRecords,
        prose: prose(),
        chapterIndex: 1,
        sectionIndex: 0,
      },
    };
    compendiumService.getRecord.and.callFake((id) => of(fullRecord(id)));

    TestBed.configureTestingModule({
      providers: [
        { provide: CompendiumService, useValue: compendiumService },
        { provide: DynamicDialogRef, useValue: dialogRef },
        { provide: DynamicDialogConfig, useValue: config },
        { provide: ToastrService, useValue: toastr },
      ],
    });

    component = createComponent();
  });

  it('clones editor state without mutating the supplied overrides', () => {
    const source = [override('Silver cloak')];
    component = createComponent(source);

    component.ngOnInit();
    component.overrides[0].description = 'Changed';
    component.overrides[0].isExpanded = true;

    expect(source).toEqual([override('Silver cloak')]);
    expect(component.overrides[0]).toEqual(
      jasmine.objectContaining({
        description: 'Changed',
        isExpanded: true,
        previousDescription: 'Red cloak',
      }),
    );
  });

  it('initializes empty override and record arrays defensively', () => {
    config.data = {
      recordOverrides: null as never,
      availableRecords: null as never,
      prose: prose(),
      chapterIndex: 1,
      sectionIndex: 0,
    };
    component = TestBed.runInInjectionContext(
      () => new RecordOverridesEditorComponent(),
    );

    component.ngOnInit();

    expect(component.overrides).toEqual([]);
    expect(component.availableRecords).toEqual([]);
  });

  it('loads contexts for initialized overrides and extracts original values', () => {
    component = createComponent([override('New goal', 'goal')], 0, 0);

    component.ngOnInit();

    expect(compendiumService.getRecord).toHaveBeenCalledOnceWith('aria');
    expect(component.suggestedKeywords).toEqual(['appearance', 'goal']);
    expect(component.overrides[0].previousDescription).toBe('Win');
  });

  it('adds an expanded override using the first available record', () => {
    component.ngOnInit();

    component.addOverride();

    expect(component.overrides).toEqual([
      jasmine.objectContaining({
        compendiumRecordId: 'aria',
        keyword: '',
        description: '',
        isExpanded: true,
      }),
    ]);
    expect(compendiumService.getRecord).toHaveBeenCalledOnceWith('aria');
  });

  it('adds an empty-record override when no records are available', () => {
    config.data.availableRecords = [];
    component = TestBed.runInInjectionContext(
      () => new RecordOverridesEditorComponent(),
    );
    component.ngOnInit();

    component.addOverride();

    expect(component.overrides[0].compendiumRecordId).toBe('');
    expect(component.suggestedKeywords).toEqual([]);
  });

  it('toggles expansion and removes overrides by index', () => {
    component = createComponent([override('One'), override('Two', 'goal')]);
    component.ngOnInit();

    component.toggleExpand(0);
    expect(component.overrides[0].isExpanded).toBeTrue();
    component.toggleExpand(0);
    expect(component.overrides[0].isExpanded).toBeFalse();

    component.removeOverride(0);
    expect(component.overrides.length).toBe(1);
    expect(component.overrides[0].keyword).toBe('goal');
  });

  it('returns display names and a fallback for unknown records', () => {
    component.ngOnInit();

    expect(component.getRecordName('tower')).toBe('The Tower');
    expect(component.getRecordName('missing')).toBe('Unknown Record');
  });

  it('resets keyword state and reloads context after a record change', () => {
    component = createComponent([override('New', 'goal')]);
    component.ngOnInit();
    const editable = component.overrides[0];
    editable.compendiumRecordId = 'tower';

    component.onRecordChange(editable);

    expect(editable.keyword).toBe('');
    expect(editable.previousDescription).toBe('');
    expect(compendiumService.getRecord).toHaveBeenCalledWith('tower');
  });

  it('parses multiline regions and filters keywords case-insensitively', () => {
    compendiumService.getRecord.and.returnValue(
      of(
        fullRecord(
          'aria',
          '[Appearance]\nTall and brave\n[/Appearance]\n[History]Old[/History]',
        ),
      ),
    );

    component.fetchSuggestedKeywords('aria');
    component.searchKeywords({ query: 'is' } as never);

    expect(component.suggestedKeywords).toEqual(['Appearance', 'History']);
    expect(component.filteredKeywords).toEqual(['History']);
  });

  it('uses cached context without another service request', () => {
    component.fetchSuggestedKeywords('aria');
    compendiumService.getRecord.calls.reset();

    component.fetchSuggestedKeywords('aria');

    expect(compendiumService.getRecord).not.toHaveBeenCalled();
    expect(component.suggestedKeywords).toEqual(['appearance', 'goal']);
  });

  it('clears suggestions for an empty record id', () => {
    component.suggestedKeywords = ['old'];
    component.filteredKeywords = ['old'];

    component.fetchSuggestedKeywords('');

    expect(component.suggestedKeywords).toEqual([]);
    expect(component.filteredKeywords).toEqual([]);
  });

  it('keeps only the latest requested record suggestions', () => {
    const ariaResponse = new Subject<CompendiumRecordDto>();
    const towerResponse = new Subject<CompendiumRecordDto>();
    compendiumService.getRecord.and.callFake((id) =>
      id === 'aria' ? ariaResponse : towerResponse,
    );

    component.fetchSuggestedKeywords('aria');
    component.fetchSuggestedKeywords('tower');
    towerResponse.next(fullRecord('tower', '[floor]Stone[/floor]'));
    ariaResponse.next(fullRecord('aria', '[goal]Win[/goal]'));

    expect(component.suggestedKeywords).toEqual(['floor']);
  });

  it('reports the current record-context request failure', () => {
    compendiumService.getRecord.and.returnValue(
      throwError(() => new Error('failed')),
    );
    component.suggestedKeywords = ['old'];

    component.fetchSuggestedKeywords('aria');

    expect(component.suggestedKeywords).toEqual([]);
    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Failed to load record context.',
    );
  });

  it('does not let an obsolete context failure clear newer suggestions', () => {
    const oldResponse = new Subject<CompendiumRecordDto>();
    compendiumService.getRecord.and.returnValues(
      oldResponse,
      of(fullRecord('tower', '[floor]Stone[/floor]')),
    );

    component.fetchSuggestedKeywords('aria');
    component.fetchSuggestedKeywords('tower');
    oldResponse.error(new Error('old failed'));

    expect(component.suggestedKeywords).toEqual(['floor']);
    expect(toastr.error).not.toHaveBeenCalled();
  });

  it('finds the latest prior description across chapters and sections', () => {
    component = createComponent([override('New cloak')]);

    component.ngOnInit();

    expect(component.overrides[0].previousDescription).toBe('Red cloak');
  });

  it('uses earlier overrides in the current section in sequence', () => {
    component = createComponent([
      override('Green cloak'),
      override('Gold cloak'),
    ]);

    component.ngOnInit();

    expect(component.overrides[0].previousDescription).toBe('Red cloak');
    expect(component.overrides[1].previousDescription).toBe('Green cloak');
  });

  it('matches record keywords case-insensitively and escapes regex characters', () => {
    compendiumService.getRecord.and.returnValue(
      of(fullRecord('aria', '[name+title]Captain[/name+title]')),
    );
    component = createComponent(
      [override('Commander', ' NAME+TITLE ')],
      0,
      0,
    );

    component.ngOnInit();

    expect(component.overrides[0].previousDescription).toBe('Captain');
  });

  it('refreshes following previous descriptions after edits and removal', () => {
    component = createComponent([
      override('Green cloak'),
      override('Gold cloak'),
    ]);
    component.ngOnInit();
    component.overrides[0].description = 'Purple cloak';

    component.onDescriptionChange();
    expect(component.overrides[1].previousDescription).toBe('Purple cloak');

    component.removeOverride(0);
    expect(component.overrides[0].previousDescription).toBe('Red cloak');
  });

  it('describes missing selections and empty previous values clearly', () => {
    component.ngOnInit();
    const editable = {
      ...override(''),
      previousDescription: '',
    };

    editable.compendiumRecordId = '';
    expect(component.getPreviousDescriptionMessage(editable)).toContain(
      'Select a record',
    );
    editable.compendiumRecordId = 'aria';
    editable.keyword = ' ';
    expect(component.getPreviousDescriptionMessage(editable)).toContain(
      'Select a keyword',
    );
    editable.keyword = 'unknown';
    expect(component.getPreviousDescriptionMessage(editable)).toContain(
      'No existing value',
    );
    editable.previousDescription = ' Existing ';
    expect(component.getPreviousDescriptionMessage(editable)).toBe(
      ' Existing ',
    );
  });

  it('rejects duplicate record/keyword pairs after normalization', () => {
    component = createComponent([
      override('One', ' Goal '),
      override('Two', 'goal'),
    ]);
    component.ngOnInit();

    component.save();

    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Duplicate override for "Aria" / "goal".',
    );
    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('allows incomplete pairs and closes with editor-only state removed', () => {
    component = createComponent([
      override('One', ''),
      override('Two', 'goal', ''),
    ]);
    component.ngOnInit();
    component.overrides[0].isExpanded = true;
    component.overrides[0].previousDescription = 'Previous';

    component.save();

    expect(dialogRef.close).toHaveBeenCalledOnceWith([
      override('One', ''),
      override('Two', 'goal', ''),
    ]);
  });

  it('closes without a result on cancel', () => {
    component.cancel();

    expect(dialogRef.close).toHaveBeenCalledOnceWith();
  });
});
