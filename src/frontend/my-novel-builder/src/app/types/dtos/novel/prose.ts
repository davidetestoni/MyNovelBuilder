export interface Prose {
  chapters: Chapter[];
}

export interface Chapter {
  title: string;
  sections: Section[];
}

export interface Section {
  summary: string;
  items: SectionItem[];
}

export interface SectionItem {
  $type: SectionItemType;
}

export interface TextSectionItem extends SectionItem {
  $type: SectionItemType.Text;
  text: string;
}

export interface ImageSectionItem extends SectionItem {
  $type: SectionItemType.Image;
  imageId: string;
}

export enum SectionItemType {
  Text = 'text',
  Image = 'image',
}
