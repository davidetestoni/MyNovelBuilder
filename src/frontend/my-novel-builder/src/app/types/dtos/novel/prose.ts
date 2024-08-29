export interface Prose {
  chapters: Chapter[];
}

export interface Chapter {
  title: string;
  sections: Section[];
}

export interface Section {
  summary: string;
  text: string;
}
