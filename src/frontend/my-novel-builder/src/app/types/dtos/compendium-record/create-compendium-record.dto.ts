import { CompendiumRecordType } from '../../enums/compendium-record-type';

export interface CreateCompendiumRecordDto {
  name: string;
  aliases: string;
  type: CompendiumRecordType;
  context: string;
  compendiumId: string;
  alwaysIncluded: boolean;
}
