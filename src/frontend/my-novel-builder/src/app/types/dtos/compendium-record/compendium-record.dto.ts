import { CompendiumRecordType } from '../../enums/compendium-record-type';
import { CompendiumRecordMediaDto } from './compendium-record-media.dto';

export interface CompendiumRecordDto {
  id: string;
  createdAt: string;
  updatedAt: string;
  name: string;
  aliases: string;
  type: CompendiumRecordType;
  context: string;
  media: CompendiumRecordMediaDto[];
  compendiumId: string;
  alwaysIncluded: boolean;
}
