export interface Prose {
  chapters: Chapter[];
}

export interface Chapter {
  title: string;
  sections: Section[];
  storyEvents: StoryEvent[];
}

export interface Section {
  summary: string;
  text: string;
  images: string[];
  recordOverrides: RecordOverride[];
}

export interface RecordOverride {
  compendiumRecordId: string;
  keyword: string;
  description: string;
}

export interface StoryEvent {
  title: string;
  date: string;
  description: string;
}
