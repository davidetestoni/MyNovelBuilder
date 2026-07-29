import { TestBed } from '@angular/core/testing';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { of, Subject } from 'rxjs';
import { CreateCompendiumComponent } from '../../components/create-compendium/create-compendium.component';
import { CompendiumService } from '../../services/compendium.service';
import type { CompendiumRecordOverviewDto } from '../../types/dtos/compendium-record/compendium-record-overview.dto';
import type { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { CompendiaComponent } from './compendia.component';

describe('CompendiaComponent workflows', () => {
  let component: CompendiaComponent;
  let compendiumService: jasmine.SpyObj<CompendiumService>;
  let dialogService: jasmine.SpyObj<DialogService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let dialogClosed: Subject<boolean | undefined>;

  const record = (
    id: string,
    name: string,
    type = CompendiumRecordType.Character,
  ): CompendiumRecordOverviewDto => ({
    id,
    name,
    type,
    imageUrl: null,
  });

  const compendium = (
    id: string,
    overrides: Partial<CompendiumDto> = {},
  ): CompendiumDto => ({
    id,
    createdAt: '2026-07-01T12:00:00Z',
    updatedAt: '2026-07-20T12:00:00Z',
    name: `Compendium ${id}`,
    description: `Description ${id}`,
    records: [],
    ...overrides,
  });

  beforeEach(() => {
    compendiumService = jasmine.createSpyObj<CompendiumService>(
      'CompendiumService',
      ['getCompendia'],
    );
    dialogService = jasmine.createSpyObj<DialogService>('DialogService', [
      'open',
    ]);
    dialogClosed = new Subject<boolean | undefined>();
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>(
      'DynamicDialogRef',
      ['close'],
      { onClose: dialogClosed.asObservable() },
    );

    compendiumService.getCompendia.and.returnValue(of([]));
    dialogService.open.and.returnValue(dialogRef);

    TestBed.configureTestingModule({
      providers: [
        { provide: CompendiumService, useValue: compendiumService },
        { provide: DialogService, useValue: dialogService },
      ],
    });

    component = TestBed.runInInjectionContext(() => new CompendiaComponent());
  });

  it('starts in the loading state and loads compendia on initialization', () => {
    const compendia = [compendium('one')];
    compendiumService.getCompendia.and.returnValue(of(compendia));

    expect(component.compendia).toBeNull();

    component.ngOnInit();

    expect(compendiumService.getCompendia).toHaveBeenCalledTimes(1);
    expect(component.compendia).toEqual(compendia);
  });

  it('keeps the loading state until the request responds', () => {
    const response = new Subject<CompendiumDto[]>();
    compendiumService.getCompendia.and.returnValue(response);

    component.getCompendia();
    expect(component.compendia).toBeNull();

    response.next([compendium('later')]);
    expect(component.compendia).toEqual([compendium('later')]);
  });

  it('sorts compendia by most recent update without mutating the response', () => {
    const oldest = compendium('oldest', {
      updatedAt: '2026-07-01T12:00:00Z',
    });
    const newest = compendium('newest', {
      updatedAt: '2026-07-29T12:00:00Z',
    });
    const middle = compendium('middle', {
      updatedAt: '2026-07-15T12:00:00Z',
    });
    const response = [oldest, newest, middle];
    compendiumService.getCompendia.and.returnValue(of(response));

    component.getCompendia();

    expect(component.compendia).toEqual([newest, middle, oldest]);
    expect(component.compendia).not.toBe(response);
    expect(response).toEqual([oldest, newest, middle]);
  });

  it('returns null filtered results while compendia are loading', () => {
    component.filter = 'anything';

    expect(component.filteredCompendia).toBeNull();
  });

  it('returns the loaded collection directly when the filter is empty', () => {
    const compendia = [compendium('one')];
    component.compendia = compendia;

    expect(component.filteredCompendia).toBe(compendia);
  });

  it('filters compendia by name without case sensitivity', () => {
    const matching = compendium('match', { name: 'Ancient MYTHS' });
    component.compendia = [matching, compendium('other')];
    component.filter = 'myths';

    expect(component.filteredCompendia).toEqual([matching]);
  });

  it('filters compendia by description without case sensitivity', () => {
    const matching = compendium('match', {
      description: 'Contains DRAGON lore',
    });
    component.compendia = [compendium('other'), matching];
    component.filter = 'dragon';

    expect(component.filteredCompendia).toEqual([matching]);
  });

  it('filters compendia by record name without case sensitivity', () => {
    const matching = compendium('match', {
      records: [record('hero', 'Lady ARIA')],
    });
    component.compendia = [matching, compendium('other')];
    component.filter = 'aria';

    expect(component.filteredCompendia).toEqual([matching]);
  });

  it('returns no compendia when nothing matches the filter', () => {
    component.compendia = [compendium('one'), compendium('two')];
    component.filter = 'missing';

    expect(component.filteredCompendia).toEqual([]);
  });

  it('previews only the first three records of a requested type', () => {
    const source = compendium('world', {
      records: [
        record('one', 'One'),
        record('place', 'A place', CompendiumRecordType.Place),
        record('two', 'Two'),
        record('three', 'Three'),
        record('four', 'Four'),
      ],
    });

    expect(
      component.getRecordsOfType(source, CompendiumRecordType.Character),
    ).toEqual([
      record('one', 'One'),
      record('two', 'Two'),
      record('three', 'Three'),
    ]);
  });

  it('shows all matching records of a requested type while filtering', () => {
    const source = compendium('world', {
      records: [
        record('one', 'Alpha'),
        record('two', 'Beta'),
        record('three', 'Gamma'),
        record('four', 'Delta'),
        record('place', 'Alpha place', CompendiumRecordType.Place),
      ],
    });
    component.filter = 'A';

    expect(
      component.getRecordsOfType(source, CompendiumRecordType.Character),
    ).toEqual([
      record('one', 'Alpha'),
      record('two', 'Beta'),
      record('three', 'Gamma'),
      record('four', 'Delta'),
    ]);
  });

  it('opens the create dialog with the expected modal settings', () => {
    component.openCreateCompendiumDialog();

    expect(dialogService.open).toHaveBeenCalledOnceWith(
      CreateCompendiumComponent,
      {
        header: 'Create New Compendium',
        width: '50vw',
        contentStyle: { overflow: 'auto' },
        baseZIndex: 10000,
        modal: true,
        closable: true,
        closeOnEscape: true,
        dismissableMask: true,
      },
    );
  });

  it('reloads compendia only after successful creation', () => {
    component.openCreateCompendiumDialog();
    compendiumService.getCompendia.calls.reset();

    dialogClosed.next(undefined);
    dialogClosed.next(false);
    expect(compendiumService.getCompendia).not.toHaveBeenCalled();

    dialogClosed.next(true);
    expect(compendiumService.getCompendia).toHaveBeenCalledTimes(1);
  });

  it('closes an open create dialog when destroyed', () => {
    component.openCreateCompendiumDialog();

    component.ngOnDestroy();

    expect(dialogRef.close).toHaveBeenCalledTimes(1);
  });

  it('can be destroyed without opening a dialog', () => {
    expect(() => component.ngOnDestroy()).not.toThrow();
    expect(dialogRef.close).not.toHaveBeenCalled();
  });
});
