import { CompendiumRecordType } from '../../enums/compendium-record-type';

export interface CompendiumRecordOverviewDto {
  id: string;
  name: string;
  type: CompendiumRecordType;
  imageUrl: string | null;
}
