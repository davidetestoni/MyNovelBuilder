import { CompendiumRecordType } from '../../enums/compendium-record-type';
import { CharacterVoiceAssignmentDto } from './character-voice-assignment.dto';

export interface CreateCompendiumRecordDto {
  name: string;
  aliases: string;
  type: CompendiumRecordType;
  context: string;
  compendiumId: string;
  alwaysIncluded: boolean;
  characterVoiceAssignments: CharacterVoiceAssignmentDto[];
}
