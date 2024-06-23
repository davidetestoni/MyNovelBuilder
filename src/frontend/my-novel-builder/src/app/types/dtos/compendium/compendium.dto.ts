import { CompendiumRecordOverviewDto } from '../compendium-record/compendium-record-overview.dto';

export interface CompendiumDto {
  id: string;
  createdAt: string;
  updatedAt: string;
  name: string;
  description: string;
  records: CompendiumRecordOverviewDto[];
}
