import type Quill from 'quill';
import {
  appendMarkdownToHtml,
  calculateReadingTimeMinutes,
  countChapterWords,
  htmlToPlainText,
  insertMarkdownIntoEditor,
  normalizeHtmlText,
  normalizeQuillHtmlWhitespace,
} from './prose-text.utils';

describe('prose text utilities', () => {
  it('converts HTML to normalized plain text without merging paragraphs', () => {
    expect(htmlToPlainText('<p>Hello</p><p>world</p>')).toBe('Hello world');
    expect(normalizeHtmlText('<p>  Hello   world  </p>')).toBe('Hello world');
  });

  it('restores breakable spaces in HTML saved by Quill', () => {
    expect(
      normalizeQuillHtmlWhitespace(
        '<p>One&nbsp;two&#160;three&#xA0;four\u00a0five</p>',
      ),
    ).toBe('<p>One two three four five</p>');
  });

  it('counts words across sections and handles empty chapters', () => {
    const chapter = {
      title: 'Chapter',
      sections: [
        { text: '<p>One two</p>', summary: '', images: [], recordOverrides: [] },
        {
          text: '<p>three</p><p>four</p>',
          summary: '',
          images: [],
          recordOverrides: [],
        },
      ],
      storyEvents: [],
    };

    expect(countChapterWords(chapter)).toBe(4);
    expect(countChapterWords({ ...chapter, sections: [] })).toBe(0);
  });

  it('rounds reading time up using the prose reading speed', () => {
    expect(calculateReadingTimeMinutes(0)).toBe(0);
    expect(calculateReadingTimeMinutes(238)).toBe(1);
    expect(calculateReadingTimeMinutes(239)).toBe(2);
  });

  it('converts and appends generated markdown', async () => {
    const result = await appendMarkdownToHtml(
      '<p>Existing</p>',
      '**Generated**',
    );

    expect(result).toContain('<p>Existing</p>');
    expect(result).toContain('<strong>Generated</strong>');
  });

  it('inserts converted markdown into Quill as user content', async () => {
    const dangerouslyPasteHTML = jasmine.createSpy('dangerouslyPasteHTML');
    const editor = {
      clipboard: { dangerouslyPasteHTML },
    } as unknown as Quill;

    await insertMarkdownIntoEditor(editor, 4, '_Generated_');

    expect(dangerouslyPasteHTML).toHaveBeenCalledOnceWith(
      4,
      jasmine.stringContaining('<em>Generated</em>'),
      'user',
    );
  });
});
