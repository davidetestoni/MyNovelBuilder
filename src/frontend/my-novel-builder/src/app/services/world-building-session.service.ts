import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { TextGenerationPreviewDto } from '../types/dtos/generate/text-generation-preview.dto';
import { CreateWorldBuildingSessionDto } from '../types/dtos/world-building/create-world-building-session.dto';
import { SendWorldBuildingMessageDto } from '../types/dtos/world-building/send-world-building-message.dto';
import { UpdateWorldBuildingProposalDto } from '../types/dtos/world-building/update-world-building-proposal.dto';
import { UpdateWorldBuildingSessionDto } from '../types/dtos/world-building/update-world-building-session.dto';
import {
  WorldBuildingSession,
  WorldBuildingSessionMetadata,
} from '../types/dtos/world-building/world-building-session';

@Injectable()
export abstract class WorldBuildingSessionService {
  abstract getSessions(): Observable<WorldBuildingSessionMetadata[]>;
  abstract getSession(sessionId: string): Observable<WorldBuildingSession>;
  abstract createSession(
    dto: CreateWorldBuildingSessionDto,
  ): Observable<WorldBuildingSession>;
  abstract updateSession(
    sessionId: string,
    dto: UpdateWorldBuildingSessionDto,
  ): Observable<void>;
  abstract deleteSession(sessionId: string): Observable<void>;
  abstract sendMessage(
    sessionId: string,
    dto: SendWorldBuildingMessageDto,
  ): Observable<WorldBuildingSession>;
  abstract getMessagePreview(
    sessionId: string,
    dto: SendWorldBuildingMessageDto,
  ): Observable<TextGenerationPreviewDto>;
  abstract deleteMessage(
    sessionId: string,
    messageId: string,
  ): Observable<WorldBuildingSession>;
  abstract updateProposal(
    sessionId: string,
    proposalId: string,
    dto: UpdateWorldBuildingProposalDto,
  ): Observable<WorldBuildingSession>;
  abstract acceptProposal(
    sessionId: string,
    proposalId: string,
  ): Observable<WorldBuildingSession>;
  abstract rejectProposal(
    sessionId: string,
    proposalId: string,
  ): Observable<WorldBuildingSession>;
}
