import { CompendiumRecordType } from '../../enums/compendium-record-type';
import { CharacterVoiceAssignmentDto } from './character-voice-assignment.dto';
import { CompendiumRecordMediaDto } from './compendium-record-media.dto';

export interface CompendiumRecordDto {
  id: string;
  createdAt: string;
  updatedAt: string;
  name: string;
  aliases: string;
  type: CompendiumRecordType;
  context: string;
  contextTokenCount: number;
  media: CompendiumRecordMediaDto[];
  compendiumId: string;
  alwaysIncluded: boolean;
  characterVoiceAssignments: CharacterVoiceAssignmentDto[];
}
