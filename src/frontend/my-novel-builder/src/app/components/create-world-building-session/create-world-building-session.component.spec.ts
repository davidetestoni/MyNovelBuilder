import { TestBed } from '@angular/core/testing';
import { DynamicDialogRef } from 'primeng/dynamicdialog';
import { of, Subject } from 'rxjs';
import { CompendiumService } from '../../services/compendium.service';
import { NovelService } from '../../services/novel.service';
import { WorldBuildingSessionService } from '../../services/world-building-session.service';
import type { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import type { NovelDto } from '../../types/dtos/novel/novel.dto';
import type { WorldBuildingSession } from '../../types/dtos/world-building/world-building-session';
import { CreateWorldBuildingSessionComponent } from './create-world-building-session.component';

describe('CreateWorldBuildingSessionComponent workflow', () => {
  let component: CreateWorldBuildingSessionComponent;
  let novelService: jasmine.SpyObj<NovelService>;
  let compendiumService: jasmine.SpyObj<CompendiumService>;
  let sessionService: jasmine.SpyObj<WorldBuildingSessionService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;

  const createdSession = (): WorldBuildingSession => ({
    id: 'created',
    name: 'New session',
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    context: {
      novelId: 'novel-id',
      chapterIndex: null,
      compendiumIds: ['compendium-id'],
      compendiumRecordIds: [],
      freeformPremise: 'A premise',
    },
    messages: [],
    proposals: [],
  });

  beforeEach(() => {
    novelService = jasmine.createSpyObj<NovelService>('NovelService', [
      'getNovels',
    ]);
    compendiumService = jasmine.createSpyObj<CompendiumService>(
      'CompendiumService',
      ['getCompendia'],
    );
    sessionService = jasmine.createSpyObj<WorldBuildingSessionService>(
      'WorldBuildingSessionService',
      ['createSession'],
    );
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>('DynamicDialogRef', [
      'close',
    ]);

    novelService.getNovels.and.returnValue(of([]));
    compendiumService.getCompendia.and.returnValue(of([]));

    TestBed.configureTestingModule({
      providers: [
        { provide: NovelService, useValue: novelService },
        { provide: CompendiumService, useValue: compendiumService },
        { provide: WorldBuildingSessionService, useValue: sessionService },
        { provide: DynamicDialogRef, useValue: dialogRef },
      ],
    });

    component = TestBed.runInInjectionContext(
      () => new CreateWorldBuildingSessionComponent(),
    );
  });

  it('loads novel and compendium options on initialization', () => {
    const novels = [{ id: 'novel-id', title: 'Novel' } as NovelDto];
    const compendia = [
      { id: 'compendium-id', name: 'World' } as CompendiumDto,
    ];
    novelService.getNovels.and.returnValue(of(novels));
    compendiumService.getCompendia.and.returnValue(of(compendia));

    component.ngOnInit();

    expect(novelService.getNovels).toHaveBeenCalledTimes(1);
    expect(compendiumService.getCompendia).toHaveBeenCalledTimes(1);
    expect(component.novels).toBe(novels);
    expect(component.compendia).toBe(compendia);
  });

  it('normalizes optional text and closes with the created session', () => {
    const created = createdSession();
    sessionService.createSession.and.returnValue(of(created));
    component.name = '  New session  ';
    component.novelId = 'novel-id';
    component.compendiumIds = ['compendium-id'];
    component.freeformPremise = '  A premise  ';

    component.createSession();

    expect(sessionService.createSession).toHaveBeenCalledOnceWith({
      name: 'New session',
      novelId: 'novel-id',
      chapterIndex: null,
      compendiumIds: ['compendium-id'],
      compendiumRecordIds: [],
      freeformPremise: 'A premise',
    });
    expect(dialogRef.close).toHaveBeenCalledOnceWith(created);
  });

  it('maps blank optional text to null', () => {
    sessionService.createSession.and.returnValue(of(createdSession()));
    component.name = '   ';
    component.freeformPremise = '';

    component.createSession();

    expect(sessionService.createSession).toHaveBeenCalledOnceWith(
      jasmine.objectContaining({
        name: null,
        novelId: null,
        freeformPremise: null,
      }),
    );
  });

  it('prevents duplicate creation while a request is pending', () => {
    const response = new Subject<WorldBuildingSession>();
    sessionService.createSession.and.returnValue(response);

    component.createSession();
    component.createSession();

    expect(component.isCreating).toBeTrue();
    expect(sessionService.createSession).toHaveBeenCalledTimes(1);

    response.next(createdSession());
    expect(dialogRef.close).toHaveBeenCalledOnceWith(createdSession());
  });

  it('restores creation after an error so the user can retry', () => {
    const failedResponse = new Subject<WorldBuildingSession>();
    sessionService.createSession.and.returnValues(
      failedResponse,
      of(createdSession()),
    );

    component.createSession();
    failedResponse.error(new Error('request failed'));

    expect(component.isCreating).toBeFalse();
    expect(dialogRef.close).not.toHaveBeenCalled();

    component.createSession();
    expect(sessionService.createSession).toHaveBeenCalledTimes(2);
    expect(dialogRef.close).toHaveBeenCalledOnceWith(createdSession());
  });

  it('closes the dialog without a result when cancelled', () => {
    component.cancel();

    expect(dialogRef.close).toHaveBeenCalledOnceWith();
    expect(sessionService.createSession).not.toHaveBeenCalled();
  });
});
