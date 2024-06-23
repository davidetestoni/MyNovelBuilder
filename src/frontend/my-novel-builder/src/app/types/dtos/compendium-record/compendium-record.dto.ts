import { CompendiumRecordType } from '../../enums/compendium-record-type';
import { CompendiumRecordImageDto } from './compendium-record-image.dto';

export interface CompendiumRecordDto {
  id: string;
  createdAt: string;
  updatedAt: string;
  name: string;
  aliases: string;
  type: CompendiumRecordType;
  context: string;
  images: CompendiumRecordImageDto[];
  compendiumId: string;
}
