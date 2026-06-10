import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateWorldBuildingSessionDto } from '../types/dtos/world-building/create-world-building-session.dto';
import { SendWorldBuildingMessageDto } from '../types/dtos/world-building/send-world-building-message.dto';
import { UpdateWorldBuildingProposalDto } from '../types/dtos/world-building/update-world-building-proposal.dto';
import { UpdateWorldBuildingSessionDto } from '../types/dtos/world-building/update-world-building-session.dto';
import {
  WorldBuildingSession,
  WorldBuildingSessionMetadata,
} from '../types/dtos/world-building/world-building-session';
import {
  mockedWorldBuildingSession,
  mockedWorldBuildingSessions,
} from './mocks/mock-world-building-session.data';
import { mockObservable } from './mocks/mock-utils';
import { WorldBuildingSessionService } from './world-building-session.service';

@Injectable()
export class MockWorldBuildingSessionService extends WorldBuildingSessionService {
  getSessions(): Observable<WorldBuildingSessionMetadata[]> {
    return mockObservable(mockedWorldBuildingSessions);
  }

  getSession(_sessionId: string): Observable<WorldBuildingSession> {
    return mockObservable(mockedWorldBuildingSession);
  }

  createSession(
    _dto: CreateWorldBuildingSessionDto,
  ): Observable<WorldBuildingSession> {
    return mockObservable(mockedWorldBuildingSession);
  }

  updateSession(
    _sessionId: string,
    _dto: UpdateWorldBuildingSessionDto,
  ): Observable<void> {
    return mockObservable<void>(undefined);
  }

  deleteSession(_sessionId: string): Observable<void> {
    return mockObservable<void>(undefined);
  }

  sendMessage(
    _sessionId: string,
    _dto: SendWorldBuildingMessageDto,
  ): Observable<WorldBuildingSession> {
    return mockObservable(mockedWorldBuildingSession);
  }

  deleteMessage(
    _sessionId: string,
    _messageId: string,
  ): Observable<WorldBuildingSession> {
    return mockObservable(mockedWorldBuildingSession);
  }

  updateProposal(
    _sessionId: string,
    _proposalId: string,
    _dto: UpdateWorldBuildingProposalDto,
  ): Observable<WorldBuildingSession> {
    return mockObservable(mockedWorldBuildingSession);
  }

  acceptProposal(
    _sessionId: string,
    _proposalId: string,
  ): Observable<WorldBuildingSession> {
    return mockObservable(mockedWorldBuildingSession);
  }

  rejectProposal(
    _sessionId: string,
    _proposalId: string,
  ): Observable<WorldBuildingSession> {
    return mockObservable(mockedWorldBuildingSession);
  }
}
