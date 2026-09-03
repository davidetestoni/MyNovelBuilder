import { ChatMessageRole } from '../../enums/chat-message-role';
import { CompendiumRecordType } from '../../enums/compendium-record-type';

export interface WorldBuildingSession {
  id: string;
  createdAt: string;
  updatedAt: string;
  name: string | null;
  context: WorldBuildingContext;
  messages: WorldBuildingMessage[];
  proposals: WorldBuildingProposal[];
}

export interface WorldBuildingSessionMetadata {
  id: string;
  novelId: string | null;
  createdAt: string;
  updatedAt: string;
  name: string | null;
}

export interface WorldBuildingContext {
  novelId: string | null;
  chapterIndex: number | null;
  compendiumIds: string[];
  compendiumRecordIds: string[];
  freeformPremise: string | null;
}

export interface WorldBuildingMessage {
  id: string;
  sentAt: string;
  role: ChatMessageRole;
  textContent: string;
  structuredContent?: string | null;
}

export enum WorldBuildingProposalStatus {
  Pending = 'pending',
  Accepted = 'accepted',
  Rejected = 'rejected',
}

export enum WorldBuildingOperationKind {
  CreateCompendium = 'createCompendium',
  UpdateCompendium = 'updateCompendium',
  CreateCompendiumRecord = 'createCompendiumRecord',
  UpdateCompendiumRecord = 'updateCompendiumRecord',
}

export interface WorldBuildingOperation {
  kind: WorldBuildingOperationKind;
  targetCompendiumId: string | null;
  targetRecordId: string | null;
  name: string;
  description: string;
  aliases: string;
  type: CompendiumRecordType;
  context: string;
  alwaysIncluded: boolean;
}

export interface WorldBuildingProposal {
  id: string;
  messageId: string | null;
  status: WorldBuildingProposalStatus;
  operation: WorldBuildingOperation;
  rationale: string | null;
  appliedEntityId: string | null;
  appliedAt: string | null;
}
