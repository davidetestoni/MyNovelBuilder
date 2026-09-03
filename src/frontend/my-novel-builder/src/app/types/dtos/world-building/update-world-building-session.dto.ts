import { WorldBuildingMessage } from './world-building-session';

export interface UpdateWorldBuildingSessionDto {
  name: string | null;
  novelId: string | null;
  chapterIndex: number | null;
  compendiumIds: string[];
  compendiumRecordIds: string[];
  freeformPremise: string | null;
  messages: WorldBuildingMessage[];
}
