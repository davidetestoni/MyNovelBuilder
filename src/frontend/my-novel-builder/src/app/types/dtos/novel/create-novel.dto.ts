import { WritingLanguage } from '../../enums/writing-language';
import { WritingPov } from '../../enums/writing-pov';
import { WritingTense } from '../../enums/writing-tense';

export interface CreateNovelDto {
  title: string;
  author: string;
  brief: string;
  tense: WritingTense;
  pov: WritingPov;
  language: WritingLanguage;
  mainCharacterId: string | null;
}
