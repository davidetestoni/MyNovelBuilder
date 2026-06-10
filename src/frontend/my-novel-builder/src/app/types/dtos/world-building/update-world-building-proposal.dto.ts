import { WorldBuildingOperation } from './world-building-session';

export interface UpdateWorldBuildingProposalDto {
  operation: WorldBuildingOperation;
  rationale: string | null;
}
