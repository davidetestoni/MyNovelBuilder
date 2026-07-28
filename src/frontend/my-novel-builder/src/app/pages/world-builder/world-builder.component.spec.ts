import { TestBed } from '@angular/core/testing';
import {
  ActivatedRoute,
  convertToParamMap,
  ParamMap,
  Router,
} from '@angular/router';
import { ConfirmationService } from 'primeng/api';
import type { Confirmation } from 'primeng/api';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { of, Subject } from 'rxjs';
import { CreateWorldBuildingSessionComponent } from '../../components/create-world-building-session/create-world-building-session.component';
import { WorldBuildingSessionService } from '../../services/world-building-session.service';
import type {
  WorldBuildingSession,
  WorldBuildingSessionMetadata,
} from '../../types/dtos/world-building/world-building-session';
import { WorldBuilderComponent } from './world-builder.component';

describe('WorldBuilderComponent workflows', () => {
  let component: WorldBuilderComponent;
  let sessionService: jasmine.SpyObj<WorldBuildingSessionService>;
  let confirmationService: jasmine.SpyObj<ConfirmationService>;
  let dialogService: jasmine.SpyObj<DialogService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let dialogClosed: Subject<WorldBuildingSession | undefined>;
  let router: jasmine.SpyObj<Router>;
  let routeParams: Subject<ParamMap>;
  let route: {
    paramMap: ReturnType<Subject<ParamMap>['asObservable']>;
    snapshot: { paramMap: ParamMap };
  };

  const metadata = (
    id: string,
    novelId = 'novel-id',
    name: string | null = `Session ${id}`,
  ): WorldBuildingSessionMetadata => ({
    id,
    novelId,
    name,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-02T00:00:00Z',
  });

  const session = (
    id: string,
    novelId = 'novel-id',
    name: string | null = `Session ${id}`,
  ): WorldBuildingSession => ({
    id,
    name,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-02T00:00:00Z',
    context: {
      novelId,
      chapterIndex: null,
      compendiumIds: [],
      compendiumRecordIds: [],
      freeformPremise: null,
    },
    messages: [],
    proposals: [],
  });

  const emitRoute = (id?: string): void => {
    route.snapshot.paramMap = convertToParamMap(id ? { id } : {});
    routeParams.next(route.snapshot.paramMap);
  };

  beforeEach(() => {
    sessionService = jasmine.createSpyObj<WorldBuildingSessionService>(
      'WorldBuildingSessionService',
      ['getSessions', 'getSession', 'deleteSession'],
    );
    confirmationService = jasmine.createSpyObj<ConfirmationService>(
      'ConfirmationService',
      ['confirm'],
    );
    dialogService = jasmine.createSpyObj<DialogService>('DialogService', [
      'open',
    ]);
    dialogClosed = new Subject<WorldBuildingSession | undefined>();
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>(
      'DynamicDialogRef',
      ['close'],
      { onClose: dialogClosed.asObservable() },
    );
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    routeParams = new Subject<ParamMap>();
    route = {
      paramMap: routeParams.asObservable(),
      snapshot: { paramMap: convertToParamMap({}) },
    };

    sessionService.getSessions.and.returnValue(of([]));
    sessionService.getSession.and.callFake((id) => of(session(id)));
    sessionService.deleteSession.and.returnValue(of(undefined));
    dialogService.open.and.returnValue(dialogRef);
    router.navigate.and.resolveTo(true);

    TestBed.configureTestingModule({
      providers: [
        { provide: WorldBuildingSessionService, useValue: sessionService },
        { provide: ConfirmationService, useValue: confirmationService },
        { provide: DialogService, useValue: dialogService },
        { provide: Router, useValue: router },
        { provide: ActivatedRoute, useValue: route },
      ],
    });

    component = TestBed.runInInjectionContext(() => new WorldBuilderComponent());
  });

  it('loads the session list and the routed session on initialization', () => {
    const selected = session('selected');
    route.snapshot.paramMap = convertToParamMap({ id: 'selected' });
    sessionService.getSessions.and.returnValue(
      of([metadata('selected'), metadata('other')]),
    );
    sessionService.getSession.and.returnValue(of(selected));

    component.ngOnInit();
    emitRoute('selected');

    expect(sessionService.getSessions).toHaveBeenCalledTimes(1);
    expect(sessionService.getSession).toHaveBeenCalledOnceWith('selected');
    expect(component.sessions).toEqual([
      metadata('selected'),
      metadata('other'),
    ]);
    expect(component.currentSession).toBe(selected);
    expect(component.currentSessionId).toBe('selected');
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('navigates to the first session when no route selection exists', () => {
    sessionService.getSessions.and.returnValue(
      of([metadata('first'), metadata('second')]),
    );

    component.ngOnInit();

    expect(router.navigate).toHaveBeenCalledOnceWith([
      '/world-builder',
      'first',
    ]);
  });

  it('clears the current session when the route selection is removed', () => {
    route.snapshot.paramMap = convertToParamMap({ id: 'selected' });
    sessionService.getSessions.and.returnValue(of([metadata('first')]));
    component.ngOnInit();
    emitRoute('selected');
    router.navigate.calls.reset();

    emitRoute();

    expect(component.currentSessionId).toBeNull();
    expect(component.currentSession).toBeNull();
    expect(router.navigate).toHaveBeenCalledOnceWith([
      '/world-builder',
      'first',
    ]);
  });

  it('does not reload the currently selected session', () => {
    const selected = session('selected');
    sessionService.getSession.and.returnValue(of(selected));

    component.loadSession('selected');
    component.loadSession('selected');

    expect(sessionService.getSession).toHaveBeenCalledOnceWith('selected');
    expect(component.currentSession).toBe(selected);
  });

  it('navigates when a session is selected', () => {
    component.selectSession('selected');

    expect(router.navigate).toHaveBeenCalledOnceWith([
      '/world-builder',
      'selected',
    ]);
  });

  it('deletes a session only after confirmation', () => {
    component.sessions = [metadata('target'), metadata('remaining')];
    component.currentSessionId = 'remaining';

    component.deleteSession('target');

    expect(sessionService.deleteSession).not.toHaveBeenCalled();
    const confirmation = confirmationService.confirm.calls.mostRecent()
      .args[0] as Confirmation;
    expect(confirmation).toEqual(
      jasmine.objectContaining({
        header: 'Confirm Delete',
        acceptButtonStyleClass: 'p-button-danger',
      }),
    );

    confirmation.accept?.();

    expect(sessionService.deleteSession).toHaveBeenCalledOnceWith('target');
    expect(component.sessions).toEqual([metadata('remaining')]);
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('returns to the session list after deleting the current session', () => {
    component.sessions = [metadata('selected')];
    component.currentSessionId = 'selected';

    component.deleteSession('selected');
    const confirmation = confirmationService.confirm.calls.mostRecent()
      .args[0] as Confirmation;
    confirmation.accept?.();

    expect(component.sessions).toEqual([]);
    expect(router.navigate).toHaveBeenCalledOnceWith(['/world-builder']);
  });

  it('opens the creation dialog with the expected modal configuration', () => {
    component.openCreateSessionDialog();

    expect(dialogService.open).toHaveBeenCalledOnceWith(
      CreateWorldBuildingSessionComponent,
      {
        header: 'Create World Builder Session',
        width: '520px',
        modal: true,
        closable: true,
        closeOnEscape: true,
        dismissableMask: true,
      },
    );

    dialogClosed.next(undefined);
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('prepends a created session and navigates to it', () => {
    const created = session('created', 'other-novel', 'Created session');
    component.sessions = [metadata('existing')];

    component.openCreateSessionDialog();
    dialogClosed.next(created);

    expect(component.sessions).toEqual([
      {
        id: 'created',
        novelId: 'other-novel',
        name: 'Created session',
        createdAt: created.createdAt,
        updatedAt: created.updatedAt,
      },
      metadata('existing'),
    ]);
    expect(router.navigate).toHaveBeenCalledOnceWith([
      '/world-builder',
      'created',
    ]);
  });

  it('creates the session list when a dialog result arrives before list loading', () => {
    const created = session('created');
    component.sessions = null;

    component.openCreateSessionDialog();
    dialogClosed.next(created);

    expect(
      component.sessions as WorldBuildingSessionMetadata[] | null,
    ).toEqual([
      jasmine.objectContaining({ id: 'created' }),
    ]);
  });

  it('synchronizes changed detail fields into session metadata', () => {
    component.sessions = [metadata('selected', 'old-novel', 'Old name')];
    const updated = session('selected', 'new-novel', 'New name');
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date('2026-07-28T12:00:00Z'));

    try {
      component.updateLocalSessionMetadata(updated);
    } finally {
      jasmine.clock().uninstall();
    }

    expect(component.currentSession).toBe(updated);
    expect(component.sessions[0]).toEqual({
      ...metadata('selected', 'new-novel', 'New name'),
      updatedAt: '2026-07-28T12:00:00.000Z',
    });
  });

  it('leaves absent metadata untouched while retaining the updated detail', () => {
    const existing = metadata('other');
    component.sessions = [existing];
    const updated = session('missing');

    component.updateLocalSessionMetadata(updated);

    expect(component.currentSession).toBe(updated);
    expect(component.sessions).toEqual([existing]);

    component.sessions = null;
    component.updateLocalSessionMetadata(session('another'));
    expect(component.sessions).toBeNull();
  });

  it('formats a relative last-updated value', () => {
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date('2026-07-28T12:00:00Z'));

    try {
      expect(
        component.getLastUpdated({
          ...metadata('session'),
          updatedAt: '2026-07-28T11:00:00Z',
        }),
      ).toBe('an hour ago');
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('closes an open creation dialog when destroyed', () => {
    component.openCreateSessionDialog();

    component.ngOnDestroy();

    expect(dialogRef.close).toHaveBeenCalledTimes(1);
  });
});
