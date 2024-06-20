import { CompendiumRecordType } from '../../enums/compendium-record-type';

export interface UpdateCompendiumRecordDto {
  id: string;
  name: string;
  aliases: string;
  type: CompendiumRecordType;
  context: string;
}
