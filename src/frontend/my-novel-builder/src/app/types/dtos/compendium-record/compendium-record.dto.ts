import { CompendiumRecordType } from '../../enums/compendium-record-type';

export interface CompendiumRecordDto {
  createdAt: string;
  updatedAt: string;
  name: string;
  aliases: string;
  type: CompendiumRecordType;
  context: string;
  currentImageId: string | null;
  compendiumId: string;
}
