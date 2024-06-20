import { WritingLanguage } from '../../enums/writing-language';
import { WritingPov } from '../../enums/writing-pov';
import { WritingTense } from '../../enums/writing-tense';

export interface NovelDto {
  createdAt: string;
  updatedAt: string;
  title: string;
  author: string;
  brief: string;
  tense: WritingTense;
  pov: WritingPov;
  language: WritingLanguage;
  mainCharacterId: string | null;
  compendiumIds: string[];
}
