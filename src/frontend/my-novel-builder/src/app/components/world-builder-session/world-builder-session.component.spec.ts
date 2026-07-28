import { SimpleChange } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ToastrService } from 'ngx-toastr';
import { ConfirmationService } from 'primeng/api';
import { DialogService } from 'primeng/dynamicdialog';
import { of, throwError } from 'rxjs';
import { CompendiumService } from '../../services/compendium.service';
import { LocalStorageService } from '../../services/local-storage.service';
import { NovelService } from '../../services/novel.service';
import { WorldBuildingSessionService } from '../../services/world-building-session.service';
import type { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import type { NovelDto } from '../../types/dtos/novel/novel.dto';
import type { Prose } from '../../types/dtos/novel/prose';
import {
  WorldBuildingOperationKind,
  WorldBuildingProposal,
  WorldBuildingProposalStatus,
  WorldBuildingSession,
} from '../../types/dtos/world-building/world-building-session';
import { ChatMessageRole } from '../../types/enums/chat-message-role';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { WorldBuilderSessionComponent } from './world-builder-session.component';

describe('WorldBuilderSessionComponent context and proposals', () => {
  let component: WorldBuilderSessionComponent;
  let novelService: jasmine.SpyObj<NovelService>;
  let compendiumService: jasmine.SpyObj<CompendiumService>;
  let sessionService: jasmine.SpyObj<WorldBuildingSessionService>;
  let localStorageService: jasmine.SpyObj<LocalStorageService>;
  let toastrService: jasmine.SpyObj<ToastrService>;
  let confirmationService: jasmine.SpyObj<ConfirmationService>;
  let dialogService: jasmine.SpyObj<DialogService>;

  const prose: Prose = {
    chapters: [
      {
        title: 'Opening',
        sections: [],
        storyEvents: [],
      },
      {
        title: 'Reckoning',
        sections: [],
        storyEvents: [],
      },
    ],
  };

  const compendia: CompendiumDto[] = [
    {
      id: 'places',
      name: 'Places',
      description: '',
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-01T00:00:00Z',
      records: [
        {
          id: 'castle',
          name: 'Castle',
          type: CompendiumRecordType.Place,
          imageUrl: null,
        },
      ],
    },
    {
      id: 'people',
      name: 'People',
      description: '',
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-01T00:00:00Z',
      records: [
        {
          id: 'hero',
          name: 'Hero',
          type: CompendiumRecordType.Character,
          imageUrl: null,
        },
        {
          id: 'castle',
          name: 'Castle duplicate',
          type: CompendiumRecordType.Place,
          imageUrl: null,
        },
      ],
    },
  ];

  const proposal = (
    id: string,
    kind = WorldBuildingOperationKind.CreateCompendium,
    status = WorldBuildingProposalStatus.Pending,
  ): WorldBuildingProposal => ({
    id,
    messageId: 'assistant-message',
    status,
    operation: {
      kind,
      targetCompendiumId: null,
      targetRecordId: null,
      name: `Proposal ${id}`,
      description: 'Description',
      aliases: '',
      type: CompendiumRecordType.Other,
      context: 'Context',
      alwaysIncluded: false,
    },
    rationale: 'Rationale',
    appliedEntityId: null,
    appliedAt: null,
  });

  const session = (
    proposals: WorldBuildingProposal[] = [],
  ): WorldBuildingSession => ({
    id: 'session-id',
    name: 'Session',
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-02T00:00:00Z',
    context: {
      novelId: 'novel-id',
      chapterIndex: 1,
      compendiumIds: ['places'],
      compendiumRecordIds: ['castle'],
      freeformPremise: 'Premise',
    },
    messages: [
      {
        id: 'assistant-message',
        sentAt: '2026-01-02T00:00:00Z',
        role: ChatMessageRole.Assistant,
        textContent: 'Suggestions',
      },
    ],
    proposals,
  });

  beforeEach(() => {
    novelService = jasmine.createSpyObj<NovelService>('NovelService', [
      'getNovels',
      'getNovelProse',
    ]);
    compendiumService = jasmine.createSpyObj<CompendiumService>(
      'CompendiumService',
      ['getCompendia'],
    );
    sessionService = jasmine.createSpyObj<WorldBuildingSessionService>(
      'WorldBuildingSessionService',
      [
        'updateSession',
        'updateProposal',
        'acceptProposal',
        'rejectProposal',
      ],
    );
    localStorageService = jasmine.createSpyObj<LocalStorageService>(
      'LocalStorageService',
      ['setNestedStringForKey'],
    );
    toastrService = jasmine.createSpyObj<ToastrService>('ToastrService', [
      'success',
      'warning',
      'error',
    ]);
    confirmationService = jasmine.createSpyObj<ConfirmationService>(
      'ConfirmationService',
      ['confirm'],
    );
    dialogService = jasmine.createSpyObj<DialogService>('DialogService', [
      'open',
    ]);

    novelService.getNovels.and.returnValue(of([]));
    novelService.getNovelProse.and.returnValue(of(prose));
    compendiumService.getCompendia.and.returnValue(of(compendia));
    sessionService.updateSession.and.returnValue(of(undefined));
    sessionService.updateProposal.and.callFake(() => of(session()));
    sessionService.acceptProposal.and.callFake(() => of(session()));
    sessionService.rejectProposal.and.callFake(() => of(session()));

    TestBed.configureTestingModule({
      providers: [
        { provide: NovelService, useValue: novelService },
        { provide: CompendiumService, useValue: compendiumService },
        { provide: WorldBuildingSessionService, useValue: sessionService },
        { provide: LocalStorageService, useValue: localStorageService },
        { provide: ToastrService, useValue: toastrService },
        { provide: ConfirmationService, useValue: confirmationService },
        { provide: DialogService, useValue: dialogService },
      ],
    });

    component = TestBed.runInInjectionContext(
      () => new WorldBuilderSessionComponent(),
    );
    component.currentSessionId = 'session-id';
    component.currentSession = session();
  });

  it('loads novel and compendium options and derives available records', () => {
    const novels = [{ id: 'novel-id', title: 'Novel' } as NovelDto];
    novelService.getNovels.and.returnValue(of(novels));

    component.ngOnInit();

    expect(component.novels()).toBe(novels);
    expect(component.compendia()).toBe(compendia);
    expect(component.allAvailableRecords().map(({ id }) => id)).toEqual([
      'castle',
      'hero',
      'castle',
    ]);
  });

  it('resets transient state and loads prose when the session changes', () => {
    const accepted = proposal(
      'accepted',
      WorldBuildingOperationKind.CreateCompendium,
      WorldBuildingProposalStatus.Accepted,
    );
    component.currentSession = session([accepted]);
    component.failedMessageIds.add('failed');
    component.toggleProposal(accepted);
    expect(component.isProposalCollapsed(accepted)).toBeFalse();

    component.ngOnChanges({
      currentSession: new SimpleChange(null, component.currentSession, true),
    });

    expect(component.isFailedMessage('failed')).toBeFalse();
    expect(component.isProposalCollapsed(accepted)).toBeTrue();
    expect(novelService.getNovelProse).toHaveBeenCalledOnceWith('novel-id');
    expect(component.prose()).toBe(prose);
    expect(component.chapters()).toEqual([
      { label: 'Opening', value: 0 },
      { label: 'Reckoning', value: 1 },
    ]);
  });

  it('clears prose when loading the selected novel fails', () => {
    novelService.getNovelProse.and.returnValue(
      throwError(() => new Error('request failed')),
    );

    component.ngOnChanges({
      currentSession: new SimpleChange(null, component.currentSession, true),
    });

    expect(component.prose()).toBeNull();
    expect(component.chapters()).toEqual([]);
  });

  it('normalizes and persists the session name', () => {
    spyOn(component.sessionUpdated, 'emit');

    component.updateSessionName('  Renamed session  ');

    expect(component.currentSession.name).toBe('Renamed session');
    expect(sessionService.updateSession).toHaveBeenCalledOnceWith('session-id', {
      name: 'Renamed session',
      novelId: 'novel-id',
      chapterIndex: 1,
      compendiumIds: ['places'],
      compendiumRecordIds: ['castle'],
      freeformPremise: 'Premise',
      messages: component.currentSession.messages,
    });
    expect(component.sessionUpdated.emit).toHaveBeenCalledOnceWith(
      component.currentSession,
    );
  });

  it('maps a blank name and premise to null before saving', () => {
    component.updateSessionName('   ');
    component.currentSession.context.freeformPremise = '   ';
    component.updatePremise();

    expect(component.currentSession.name).toBeNull();
    expect(component.currentSession.context.freeformPremise).toBeNull();
    expect(sessionService.updateSession).toHaveBeenCalledTimes(2);
    expect(
      sessionService.updateSession.calls.mostRecent().args[1].freeformPremise,
    ).toBeNull();
  });

  it('changes the novel, clears chapter selection, and reloads prose', () => {
    component.updateNovel('other-novel');

    expect(component.currentSession.context.novelId).toBe('other-novel');
    expect(component.currentSession.context.chapterIndex).toBeNull();
    expect(novelService.getNovelProse).toHaveBeenCalledOnceWith('other-novel');
    expect(sessionService.updateSession).toHaveBeenCalledOnceWith(
      'session-id',
      jasmine.objectContaining({
        novelId: 'other-novel',
        chapterIndex: null,
      }),
    );
  });

  it('clears prose without loading when the novel is removed', () => {
    component.prose.set(prose);

    component.updateNovel(null);

    expect(component.prose()).toBeNull();
    expect(novelService.getNovelProse).not.toHaveBeenCalled();
    expect(sessionService.updateSession).toHaveBeenCalledOnceWith(
      'session-id',
      jasmine.objectContaining({
        novelId: null,
        chapterIndex: null,
      }),
    );
  });

  it('persists chapter selection', () => {
    component.updateChapter(0);

    expect(component.currentSession.context.chapterIndex).toBe(0);
    expect(sessionService.updateSession).toHaveBeenCalledOnceWith(
      'session-id',
      jasmine.objectContaining({ chapterIndex: 0 }),
    );
  });

  it('adds records from newly selected compendia without duplicate IDs', () => {
    component.compendia.set(compendia);

    component.onCompendiaChange({ value: ['places', 'people'] });

    expect(component.currentSession.context.compendiumIds).toEqual([
      'places',
      'people',
    ]);
    expect(component.currentSession.context.compendiumRecordIds).toEqual([
      'castle',
      'hero',
    ]);
    expect(sessionService.updateSession).toHaveBeenCalledOnceWith(
      'session-id',
      jasmine.objectContaining({
        compendiumIds: ['places', 'people'],
        compendiumRecordIds: ['castle', 'hero'],
      }),
    );
  });

  it('persists explicit record selection', () => {
    component.onRecordsChange({ value: ['hero'] });

    expect(component.currentSession.context.compendiumRecordIds).toEqual([
      'hero',
    ]);
    expect(sessionService.updateSession).toHaveBeenCalledOnceWith(
      'session-id',
      jasmine.objectContaining({ compendiumRecordIds: ['hero'] }),
    );
  });

  it('provides labels for operation kinds, statuses, and record types', () => {
    expect(
      Object.values(WorldBuildingOperationKind).map((kind) =>
        component.getOperationKindLabel(kind),
      ),
    ).toEqual([
      'Create compendium',
      'Update compendium',
      'Create compendium record',
      'Update compendium record',
    ]);
    expect(
      Object.values(WorldBuildingProposalStatus).map((status) =>
        component.getProposalStatusLabel(status),
      ),
    ).toEqual(['Pending', 'Accepted', 'Rejected']);
    expect(component.recordTypeOptions).toEqual(
      Object.values(CompendiumRecordType),
    );
    expect(component.getRecordTypeLabel(CompendiumRecordType.Character)).toBe(
      'Character',
    );
  });

  it('filters proposals by message and identifies their target kind', () => {
    const createRecord = proposal(
      'create-record',
      WorldBuildingOperationKind.CreateCompendiumRecord,
    );
    const updateRecord = proposal(
      'update-record',
      WorldBuildingOperationKind.UpdateCompendiumRecord,
    );
    const updateCompendium = proposal(
      'update-compendium',
      WorldBuildingOperationKind.UpdateCompendium,
    );
    updateRecord.messageId = 'other-message';
    component.currentSession.proposals = [
      createRecord,
      updateRecord,
      updateCompendium,
    ];

    expect(component.getProposalsForMessage('assistant-message')).toEqual([
      createRecord,
      updateCompendium,
    ]);
    expect(component.isRecordOperation(createRecord)).toBeTrue();
    expect(component.isRecordOperation(updateCompendium)).toBeFalse();
    expect(component.isCompendiumOperation(updateCompendium)).toBeTrue();
    expect(component.isCompendiumOperation(updateRecord)).toBeFalse();
  });

  it('collapses completed proposals by default and honors manual toggles', () => {
    const pending = proposal('pending');
    const accepted = proposal(
      'accepted',
      WorldBuildingOperationKind.CreateCompendium,
      WorldBuildingProposalStatus.Accepted,
    );

    expect(component.isProposalCollapsed(pending)).toBeFalse();
    expect(component.isProposalCollapsed(accepted)).toBeTrue();

    component.toggleProposal(pending);
    component.toggleProposal(accepted);
    expect(component.isProposalCollapsed(pending)).toBeTrue();
    expect(component.isProposalCollapsed(accepted)).toBeFalse();

    component.toggleProposal(pending);
    expect(component.isProposalCollapsed(pending)).toBeFalse();
  });

  it('explains when a proposal cannot be accepted', () => {
    const completed = proposal(
      'completed',
      WorldBuildingOperationKind.CreateCompendium,
      WorldBuildingProposalStatus.Accepted,
    );
    const updateCompendium = proposal(
      'update-compendium',
      WorldBuildingOperationKind.UpdateCompendium,
    );
    const createRecord = proposal(
      'create-record',
      WorldBuildingOperationKind.CreateCompendiumRecord,
    );
    const updateRecord = proposal(
      'update-record',
      WorldBuildingOperationKind.UpdateCompendiumRecord,
    );

    expect(component.getAcceptDisabledReason(completed)).toBe(
      'Only pending proposals can be accepted',
    );
    expect(component.getAcceptDisabledReason(updateCompendium)).toBe(
      'Select a target compendium before accepting',
    );
    expect(component.getAcceptDisabledReason(createRecord)).toBe(
      'Select a target compendium before accepting',
    );
    expect(component.getAcceptDisabledReason(updateRecord)).toBe(
      'Select a target record before accepting',
    );
    expect(component.canAcceptProposal(proposal('create-compendium'))).toBeTrue();

    updateCompendium.operation.targetCompendiumId = 'places';
    updateRecord.operation.targetRecordId = 'castle';
    expect(component.canAcceptProposal(updateCompendium)).toBeTrue();
    expect(component.canAcceptProposal(updateRecord)).toBeTrue();
  });

  it('saves an edited proposal and emits the updated session', () => {
    const edited = proposal('edited');
    const updated = session([edited]);
    sessionService.updateProposal.and.returnValue(of(updated));
    spyOn(component.sessionUpdated, 'emit');

    component.saveProposal(edited);

    expect(sessionService.updateProposal).toHaveBeenCalledOnceWith(
      'session-id',
      'edited',
      {
        operation: edited.operation,
        rationale: 'Rationale',
      },
    );
    expect(component.currentSession).toBe(updated);
    expect(component.sessionUpdated.emit).toHaveBeenCalledOnceWith(updated);
    expect(toastrService.success).toHaveBeenCalledOnceWith('Proposal updated');
  });

  it('warns instead of accepting an invalid proposal', () => {
    const invalid = proposal(
      'invalid',
      WorldBuildingOperationKind.CreateCompendiumRecord,
    );

    component.acceptProposal(invalid);

    expect(toastrService.warning).toHaveBeenCalledOnceWith(
      'Select a target compendium before accepting',
    );
    expect(sessionService.updateProposal).not.toHaveBeenCalled();
    expect(sessionService.acceptProposal).not.toHaveBeenCalled();
  });

  it('saves, applies, and reports an accepted proposal', () => {
    const edited = proposal('accepted');
    const saved = proposal('accepted');
    const savedSession = session([saved]);
    const acceptedSession = session([
      {
        ...saved,
        status: WorldBuildingProposalStatus.Accepted,
      },
    ]);
    sessionService.updateProposal.and.returnValue(of(savedSession));
    sessionService.acceptProposal.and.returnValue(of(acceptedSession));
    spyOn(component.sessionUpdated, 'emit');

    component.acceptProposal(edited);

    expect(sessionService.updateProposal).toHaveBeenCalledOnceWith(
      'session-id',
      'accepted',
      {
        operation: edited.operation,
        rationale: edited.rationale,
      },
    );
    expect(sessionService.acceptProposal).toHaveBeenCalledOnceWith(
      'session-id',
      'accepted',
    );
    expect(component.currentSession).toBe(acceptedSession);
    expect(component.sessionUpdated.emit).toHaveBeenCalledOnceWith(
      acceptedSession,
    );
    expect(compendiumService.getCompendia).toHaveBeenCalledTimes(1);
    expect(component.compendia()).toBe(compendia);
    expect(toastrService.success).toHaveBeenCalledOnceWith('Proposal applied');
  });

  it('retains the saved session when its proposal cannot be found', () => {
    const edited = proposal('missing');
    const savedSession = session([]);
    sessionService.updateProposal.and.returnValue(of(savedSession));
    spyOn(component.sessionUpdated, 'emit');

    component.acceptProposal(edited);

    expect(sessionService.acceptProposal).not.toHaveBeenCalled();
    expect(component.currentSession).toBe(savedSession);
    expect(component.sessionUpdated.emit).toHaveBeenCalledOnceWith(savedSession);
    expect(toastrService.error).toHaveBeenCalledOnceWith(
      'Proposal was not found after saving.',
    );
  });

  it('rejects a proposal and emits the updated session', () => {
    const rejected = proposal('rejected');
    const rejectedSession = session([
      {
        ...rejected,
        status: WorldBuildingProposalStatus.Rejected,
      },
    ]);
    sessionService.rejectProposal.and.returnValue(of(rejectedSession));
    spyOn(component.sessionUpdated, 'emit');

    component.rejectProposal(rejected);

    expect(sessionService.rejectProposal).toHaveBeenCalledOnceWith(
      'session-id',
      'rejected',
    );
    expect(component.currentSession).toBe(rejectedSession);
    expect(component.sessionUpdated.emit).toHaveBeenCalledOnceWith(
      rejectedSession,
    );
  });

  it('resolves proposal target names with readable fallbacks', () => {
    component.compendia.set(compendia);

    expect(component.getTargetCompendiumName(null)).toBe('No compendium');
    expect(component.getTargetCompendiumName('places')).toBe('Places');
    expect(component.getTargetCompendiumName('missing')).toBe('missing');
    expect(component.getTargetRecordName(null)).toBe('No record');
    expect(component.getTargetRecordName('hero')).toBe('Hero');
    expect(component.getTargetRecordName('missing')).toBe('missing');
  });
});
