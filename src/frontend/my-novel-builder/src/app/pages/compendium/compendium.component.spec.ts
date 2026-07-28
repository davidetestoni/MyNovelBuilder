import { TestBed } from '@angular/core/testing';
import {
  ActivatedRoute,
  convertToParamMap,
  type ParamMap,
  Router,
} from '@angular/router';
import { ConfirmationService } from 'primeng/api';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { of, Subject } from 'rxjs';
import { CreateCompendiumRecordComponent } from '../../components/create-compendium-record/create-compendium-record.component';
import { CompendiumService } from '../../services/compendium.service';
import type { CompendiumRecordDto } from '../../types/dtos/compendium-record/compendium-record.dto';
import type { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { CompendiumComponent } from './compendium.component';

describe('CompendiumComponent workflows', () => {
  let component: CompendiumComponent;
  let compendiumService: jasmine.SpyObj<CompendiumService>;
  let router: jasmine.SpyObj<Router>;
  let dialogService: jasmine.SpyObj<DialogService>;
  let confirmationService: jasmine.SpyObj<ConfirmationService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let dialogClosed: Subject<CompendiumRecordDto | undefined>;
  let routeParams: Subject<ParamMap>;

  const record = (
    id: string,
    name: string,
    type = CompendiumRecordType.Character,
    aliases = '',
  ): CompendiumRecordDto => ({
    id,
    createdAt: '2025-01-01T00:00:00Z',
    updatedAt: '2025-01-01T00:00:00Z',
    name,
    aliases,
    type,
    context: `${name} context`,
    contextTokenCount: 10,
    media: [],
    compendiumId: 'compendium-id',
    alwaysIncluded: false,
    characterVoiceAssignments: [],
  });

  const compendium = (): CompendiumDto => ({
    id: 'compendium-id',
    createdAt: '2025-01-01T00:00:00Z',
    updatedAt: '2025-02-01T00:00:00Z',
    name: 'Compendium',
    description: 'Description',
    records: [],
  });

  beforeEach(() => {
    compendiumService = jasmine.createSpyObj<CompendiumService>(
      'CompendiumService',
      [
        'getCompendium',
        'getRecords',
        'updateCompendium',
        'updateRecord',
        'deleteRecord',
        'deleteCompendium',
      ],
    );
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    dialogService = jasmine.createSpyObj<DialogService>('DialogService', [
      'open',
    ]);
    confirmationService = jasmine.createSpyObj<ConfirmationService>(
      'ConfirmationService',
      ['confirm'],
    );
    dialogClosed = new Subject<CompendiumRecordDto | undefined>();
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>(
      'DynamicDialogRef',
      ['close'],
      { onClose: dialogClosed.asObservable() },
    );
    routeParams = new Subject<ParamMap>();

    compendiumService.getCompendium.and.returnValue(of(compendium()));
    compendiumService.getRecords.and.returnValue(
      of([record('first', 'First'), record('second', 'Second')]),
    );
    compendiumService.updateCompendium.and.callFake((update) =>
      of({
        ...compendium(),
        name: update.name,
        description: update.description,
      }),
    );
    compendiumService.updateRecord.and.callFake((update) =>
      of({
        ...record(update.id, update.name, update.type, update.aliases),
        context: update.context,
        alwaysIncluded: update.alwaysIncluded,
        characterVoiceAssignments: update.characterVoiceAssignments,
      }),
    );
    compendiumService.deleteRecord.and.returnValue(of(undefined));
    compendiumService.deleteCompendium.and.returnValue(of(undefined));
    router.navigate.and.resolveTo(true);
    dialogService.open.and.returnValue(dialogRef);

    TestBed.configureTestingModule({
      providers: [
        { provide: CompendiumService, useValue: compendiumService },
        { provide: Router, useValue: router },
        { provide: DialogService, useValue: dialogService },
        { provide: ConfirmationService, useValue: confirmationService },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({
                id: 'compendium-id',
                recordId: 'second',
              }),
            },
            paramMap: routeParams.asObservable(),
          },
        },
      ],
    });

    component = TestBed.runInInjectionContext(
      () => new CompendiumComponent(),
    );
  });

  it('loads the route compendium and records including the selected record', () => {
    const loadedCompendium = compendium();
    const first = record('first', 'First');
    const second = record('second', 'Second');
    compendiumService.getCompendium.and.returnValue(of(loadedCompendium));
    compendiumService.getRecords.and.returnValue(of([first, second]));

    component.ngOnInit();

    expect(component.compendiumId).toBe('compendium-id');
    expect(component.compendium).toBe(loadedCompendium);
    expect(component.records).toEqual([first, second]);
    expect(component.currentRecord).toBe(second);
    expect(compendiumService.getCompendium).toHaveBeenCalledOnceWith(
      'compendium-id',
    );
    expect(compendiumService.getRecords).toHaveBeenCalledOnceWith(
      'compendium-id',
    );
  });

  it('updates record selection when route parameters change', () => {
    component.ngOnInit();

    routeParams.next(convertToParamMap({ recordId: 'first' }));
    expect(component.currentRecord?.id).toBe('first');

    routeParams.next(convertToParamMap({ recordId: 'missing' }));
    expect(component.currentRecord).toBeNull();
  });

  it('refreshes the selected record with the latest service result', () => {
    const stale = record('first', 'Stale');
    const refreshed = record('first', 'Refreshed');
    component.currentRecord = stale;
    compendiumService.getRecords.and.returnValue(of([refreshed]));

    component.getRecords();

    expect(component.currentRecord).toBe(refreshed);

    compendiumService.getRecords.and.returnValue(of([]));
    component.getRecords();
    expect(component.currentRecord).toBeNull();
  });

  it('selects a record and updates the route', () => {
    component.compendiumId = 'compendium-id';
    const selected = record('record-id', 'Selected');

    component.setCurrentRecord(selected);

    expect(component.currentRecord).toBe(selected);
    expect(router.navigate).toHaveBeenCalledOnceWith([
      '/compendium',
      'compendium-id',
      'record',
      'record-id',
    ]);
  });

  it('groups records by type when no text filter is set', () => {
    const character = record(
      'character',
      'Character',
      CompendiumRecordType.Character,
    );
    const place = record('place', 'Place', CompendiumRecordType.Place);
    component.records = [character, place];

    expect(
      component.getRecordsOfType(CompendiumRecordType.Character),
    ).toEqual([character]);
    expect(component.getRecordsOfType(CompendiumRecordType.Place)).toEqual([
      place,
    ]);

    component.records = null;
    expect(
      component.getRecordsOfType(CompendiumRecordType.Character),
    ).toEqual([]);
  });

  it('filters records case-insensitively by name or aliases', () => {
    const nameMatch = record('name', 'Ada Lovelace');
    const aliasMatch = record('alias', 'Bruce Wayne', undefined, 'BATMAN');
    const noMatch = record('other', 'Clark Kent', undefined, 'Superman');
    component.records = [nameMatch, aliasMatch, noMatch];

    component.filter = 'ada';
    expect(
      component.getRecordsOfType(CompendiumRecordType.Character),
    ).toEqual([nameMatch]);

    component.filter = 'batman';
    expect(
      component.getRecordsOfType(CompendiumRecordType.Character),
    ).toEqual([aliasMatch]);
  });

  it('returns the first current image or null when none is current', () => {
    const target = record('record', 'Record');
    target.media = [
      {
        id: 'old',
        url: '/old.png',
        isCurrent: false,
        isVideo: false,
      },
      {
        id: 'current',
        url: '/current.png',
        isCurrent: true,
        isVideo: false,
      },
      {
        id: 'also-current',
        url: '/also-current.png',
        isCurrent: true,
        isVideo: false,
      },
    ];

    expect(component.getRecordImage(target)).toBe('/current.png');

    target.media.forEach((media) => (media.isCurrent = false));
    expect(component.getRecordImage(target)).toBeNull();
  });

  it('opens the create-record dialog and refreshes a created record', () => {
    component.compendiumId = 'compendium-id';
    const created = record('created', 'Created');

    component.openCreateRecordDialog();

    expect(dialogService.open).toHaveBeenCalledOnceWith(
      CreateCompendiumRecordComponent,
      jasmine.objectContaining({
        header: 'Create Record',
        modal: true,
        data: { compendiumId: 'compendium-id' },
      }),
    );

    dialogClosed.next(undefined);
    expect(compendiumService.getRecords).not.toHaveBeenCalled();

    dialogClosed.next(created);
    expect(router.navigate).toHaveBeenCalledOnceWith([
      '/compendium',
      'compendium-id',
      'record',
      'created',
    ]);
    expect(compendiumService.getRecords).toHaveBeenCalledOnceWith(
      'compendium-id',
    );
  });

  it('closes an open create-record dialog when destroyed', () => {
    component.openCreateRecordDialog();

    component.ngOnDestroy();

    expect(dialogRef.close).toHaveBeenCalledTimes(1);
  });

  it('persists editable compendium fields and stores the response', () => {
    const loaded = compendium();
    loaded.name = 'Updated compendium';
    loaded.description = 'Updated description';
    component.compendium = loaded;

    component.updateCompendium();

    expect(compendiumService.updateCompendium).toHaveBeenCalledOnceWith({
      id: 'compendium-id',
      name: 'Updated compendium',
      description: 'Updated description',
    });
    expect(component.compendium).toEqual(
      jasmine.objectContaining({
        name: 'Updated compendium',
        description: 'Updated description',
      }),
    );
  });

  it('persists a record and replaces it in selection and collection', () => {
    const edited = record('record-id', 'Edited', undefined, 'Alias');
    edited.context = 'Updated context';
    edited.alwaysIncluded = true;
    const stale = record('record-id', 'Stale');
    component.currentRecord = stale;
    component.records = [stale, record('other', 'Other')];

    component.updateRecord(edited);

    expect(compendiumService.updateRecord).toHaveBeenCalledOnceWith({
      id: 'record-id',
      name: 'Edited',
      aliases: 'Alias',
      type: CompendiumRecordType.Character,
      context: 'Updated context',
      alwaysIncluded: true,
      characterVoiceAssignments: [],
    });
    expect(component.currentRecord).toEqual(
      jasmine.objectContaining({ id: 'record-id', name: 'Edited' }),
    );
    expect(component.records[0]).toBe(component.currentRecord);
  });

  it('updates a collection record without replacing a different selection', () => {
    const selected = record('selected', 'Selected');
    const stale = record('edited', 'Stale');
    component.currentRecord = selected;
    component.records = [selected, stale];

    component.updateRecord(record('edited', 'Updated'));

    expect(component.currentRecord).toBe(selected);
    expect(component.records[1].name).toBe('Updated');
  });

  it('deletes a record, refreshes the list, clears selection, and updates the route', () => {
    component.compendiumId = 'compendium-id';
    const target = record('target', 'Target');
    component.currentRecord = target;

    component.deleteRecord(target);

    expect(compendiumService.deleteRecord).toHaveBeenCalledOnceWith('target');
    expect(compendiumService.getRecords).toHaveBeenCalledOnceWith(
      'compendium-id',
    );
    expect(component.currentRecord).toBeNull();
    expect(router.navigate).toHaveBeenCalledOnceWith([
      '/compendium',
      'compendium-id',
    ]);
  });

  it('deletes a compendium only after confirmation and returns to the list', () => {
    component.compendium = compendium();

    component.deleteCompendium();

    expect(confirmationService.confirm).toHaveBeenCalledOnceWith(
      jasmine.objectContaining({
        message:
          'Are you sure you want to remove this compendium and all of its records? This action cannot be undone.',
        header: 'Confirm Delete',
        icon: 'pi pi-exclamation-triangle',
        acceptButtonStyleClass: 'p-button-danger',
        accept: jasmine.any(Function),
      }),
    );
    expect(compendiumService.deleteCompendium).not.toHaveBeenCalled();

    confirmationService.confirm.calls.mostRecent().args[0].accept!();

    expect(compendiumService.deleteCompendium).toHaveBeenCalledOnceWith(
      'compendium-id',
    );
    expect(router.navigate).toHaveBeenCalledOnceWith(['/compendia']);
  });

  it('guards compendium mutations until the compendium has loaded', () => {
    component.compendium = null;

    component.updateCompendium();
    component.deleteCompendium();

    expect(compendiumService.updateCompendium).not.toHaveBeenCalled();
    expect(confirmationService.confirm).not.toHaveBeenCalled();
  });
});
