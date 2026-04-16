import { CompendiumRecordType } from '../../enums/compendium-record-type';
import { CharacterVoiceAssignmentDto } from './character-voice-assignment.dto';

export interface UpdateCompendiumRecordDto {
  id: string;
  name: string;
  aliases: string;
  type: CompendiumRecordType;
  context: string;
  alwaysIncluded: boolean;
  characterVoiceAssignments: CharacterVoiceAssignmentDto[];
}
