import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { TextGenerationPreviewDto } from '../types/dtos/generate/text-generation-preview.dto';
import { environment } from '../../environment';
import { CreateWorldBuildingSessionDto } from '../types/dtos/world-building/create-world-building-session.dto';
import { SendWorldBuildingMessageDto } from '../types/dtos/world-building/send-world-building-message.dto';
import { UpdateWorldBuildingProposalDto } from '../types/dtos/world-building/update-world-building-proposal.dto';
import { UpdateWorldBuildingSessionDto } from '../types/dtos/world-building/update-world-building-session.dto';
import {
  WorldBuildingSession,
  WorldBuildingSessionMetadata,
} from '../types/dtos/world-building/world-building-session';
import { WorldBuildingSessionService } from './world-building-session.service';

@Injectable()
export class ApiWorldBuildingSessionService extends WorldBuildingSessionService {
  private http = inject(HttpClient);
  private baseUrl = environment.api.baseUrl;

  getSessions(): Observable<WorldBuildingSessionMetadata[]> {
    return this.http.get<WorldBuildingSessionMetadata[]>(
      `${this.baseUrl}/world-building-agent/sessions`,
    );
  }

  getSession(sessionId: string): Observable<WorldBuildingSession> {
    return this.http.get<WorldBuildingSession>(
      `${this.baseUrl}/world-building-agent/session/${sessionId}`,
    );
  }

  createSession(
    dto: CreateWorldBuildingSessionDto,
  ): Observable<WorldBuildingSession> {
    return this.http.post<WorldBuildingSession>(
      `${this.baseUrl}/world-building-agent/session`,
      dto,
    );
  }

  updateSession(
    sessionId: string,
    dto: UpdateWorldBuildingSessionDto,
  ): Observable<void> {
    return this.http.put<void>(
      `${this.baseUrl}/world-building-agent/session/${sessionId}`,
      dto,
    );
  }

  deleteSession(sessionId: string): Observable<void> {
    return this.http.delete<void>(
      `${this.baseUrl}/world-building-agent/session/${sessionId}`,
    );
  }

  sendMessage(
    sessionId: string,
    dto: SendWorldBuildingMessageDto,
  ): Observable<WorldBuildingSession> {
    return this.http.post<WorldBuildingSession>(
      `${this.baseUrl}/world-building-agent/session/${sessionId}/message`,
      dto,
    );
  }

  getMessagePreview(
    sessionId: string,
    dto: SendWorldBuildingMessageDto,
  ): Observable<TextGenerationPreviewDto> {
    return this.http.post<TextGenerationPreviewDto>(
      `${this.baseUrl}/world-building-agent/session/${sessionId}/message/preview`,
      dto,
    );
  }

  deleteMessage(
    sessionId: string,
    messageId: string,
  ): Observable<WorldBuildingSession> {
    return this.http.delete<WorldBuildingSession>(
      `${this.baseUrl}/world-building-agent/session/${sessionId}/message/${messageId}`,
    );
  }

  updateProposal(
    sessionId: string,
    proposalId: string,
    dto: UpdateWorldBuildingProposalDto,
  ): Observable<WorldBuildingSession> {
    return this.http.put<WorldBuildingSession>(
      `${this.baseUrl}/world-building-agent/session/${sessionId}/proposal/${proposalId}`,
      dto,
    );
  }

  acceptProposal(
    sessionId: string,
    proposalId: string,
  ): Observable<WorldBuildingSession> {
    return this.http.post<WorldBuildingSession>(
      `${this.baseUrl}/world-building-agent/session/${sessionId}/proposal/${proposalId}/accept`,
      {},
    );
  }

  rejectProposal(
    sessionId: string,
    proposalId: string,
  ): Observable<WorldBuildingSession> {
    return this.http.post<WorldBuildingSession>(
      `${this.baseUrl}/world-building-agent/session/${sessionId}/proposal/${proposalId}/reject`,
      {},
    );
  }
}
