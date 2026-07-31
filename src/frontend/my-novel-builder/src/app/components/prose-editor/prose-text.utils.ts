import { marked } from 'marked';
import type Quill from 'quill';
import type { Prose } from '../../types/dtos/novel/prose';

const averageReadingWordsPerMinute = 238;

export function htmlToPlainText(html: string): string {
  const normalizedHtml = html.replace(/<\/p>\s*<p>/g, '</p> <p>');
  const container = document.createElement('div');
  container.innerHTML = normalizedHtml;
  const text = container.innerText;
  container.remove();
  return text;
}

export function normalizeHtmlText(html: string): string {
  return htmlToPlainText(html).replace(/\s+/g, ' ').trim();
}

export function countChapterWords(
  chapter: Prose['chapters'][number],
): number {
  const text = chapter.sections
    .map((section) => normalizeHtmlText(section.text))
    .join(' ')
    .trim();

  return text ? text.split(/\s+/).length : 0;
}

export function calculateReadingTimeMinutes(wordCount: number): number {
  return wordCount === 0
    ? 0
    : Math.ceil(wordCount / averageReadingWordsPerMinute);
}

export async function markdownToHtml(markdown: string): Promise<string> {
  const html = await marked.parse(markdown);
  return typeof html === 'string' ? html : markdown;
}

export async function appendMarkdownToHtml(
  currentHtml: string,
  markdown: string,
): Promise<string> {
  return `${currentHtml}${await markdownToHtml(markdown)}`;
}

export async function insertMarkdownIntoEditor(
  editor: Quill,
  offset: number,
  markdown: string,
): Promise<void> {
  const html = await markdownToHtml(markdown);
  editor.clipboard.dangerouslyPasteHTML(offset, html, 'user');
}
