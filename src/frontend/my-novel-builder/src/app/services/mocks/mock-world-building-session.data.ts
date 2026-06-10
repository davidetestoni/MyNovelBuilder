import { WorldBuildingSession, WorldBuildingSessionMetadata, WorldBuildingOperationKind, WorldBuildingProposalStatus } from '../../types/dtos/world-building/world-building-session';
import { ChatMessageRole } from '../../types/enums/chat-message-role';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { indexToGuid } from './mock-utils';

export const mockedWorldBuildingSessions: WorldBuildingSessionMetadata[] = [
  {
    id: indexToGuid(201),
    novelId: null,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-02T00:00:00Z',
    name: 'Coastal city states',
  },
];

export const mockedWorldBuildingSession: WorldBuildingSession = {
  id: indexToGuid(201),
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-02T00:00:00Z',
  name: 'Coastal city states',
  context: {
    novelId: null,
    chapterIndex: null,
    compendiumIds: [indexToGuid(1)],
    compendiumRecordIds: [],
    freeformPremise: 'A salt-rich archipelago where trade guilds act like city governments.',
  },
  messages: [
    {
      id: indexToGuid(202),
      sentAt: '2026-01-01T00:05:00Z',
      role: ChatMessageRole.User,
      textContent: 'Help me seed the factions.',
    },
    {
      id: indexToGuid(203),
      sentAt: '2026-01-01T00:06:00Z',
      role: ChatMessageRole.Assistant,
      textContent: 'I would start with a trade order that controls salt ledgers and port access.',
    },
  ],
  proposals: [
    {
      id: indexToGuid(204),
      messageId: indexToGuid(203),
      status: WorldBuildingProposalStatus.Pending,
      operation: {
        kind: WorldBuildingOperationKind.CreateCompendiumRecord,
        targetCompendiumId: indexToGuid(1),
        targetRecordId: null,
        name: 'Saltwardens',
        description: '',
        aliases: '',
        type: CompendiumRecordType.Concept,
        context: 'A guild-backed civic order that audits salt stores, assigns harbor priority, and quietly decides which families can survive a bad season.',
        alwaysIncluded: false,
      },
      rationale: 'This gives the setting a concrete institution with political leverage.',
      appliedEntityId: null,
      appliedAt: null,
    },
  ],
};
