import { Pipe, PipeTransform } from '@angular/core';

export interface RecordContextSection {
  heading: string | null;
  text: string;
}

@Pipe({
  name: 'recordContextSections',
  standalone: true,
})
export class RecordContextSectionsPipe implements PipeTransform {
  transform(context: string | null | undefined): RecordContextSection[] {
    if (!context) {
      return [];
    }

    const sections: RecordContextSection[] = [];
    const taggedSection = /\[([^\[\]\/\r\n]+)\]([\s\S]*?)\[\/\1\]/gi;
    let end = 0;

    for (const match of context.matchAll(taggedSection)) {
      const precedingText = context.slice(end, match.index).trim();
      if (precedingText) {
        sections.push({ heading: null, text: precedingText });
      }

      sections.push({ heading: match[1].trim(), text: match[2].trim() });
      end = match.index! + match[0].length;
    }

    const remainingText = context.slice(end).trim();
    if (remainingText) {
      sections.push({ heading: null, text: remainingText });
    }

    return sections;
  }
}
