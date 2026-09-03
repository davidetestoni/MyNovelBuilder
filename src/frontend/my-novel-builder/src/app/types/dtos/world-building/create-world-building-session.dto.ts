export interface CreateWorldBuildingSessionDto {
  name: string | null;
  novelId: string | null;
  chapterIndex: number | null;
  compendiumIds: string[];
  compendiumRecordIds: string[];
  freeformPremise: string | null;
}
