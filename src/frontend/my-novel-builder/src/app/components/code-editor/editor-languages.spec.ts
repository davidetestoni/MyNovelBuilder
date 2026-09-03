import { EditorState } from '@codemirror/state';
import { EditorView } from '@codemirror/view';
import { getLanguageExtensions } from './editor-languages';

describe('editor languages', () => {
  let parent: HTMLDivElement;
  let view: EditorView | undefined;

  const createView = (documentText: string, language: 'prompt' | 'compendium' | 'plain') => {
    view = new EditorView({
      state: EditorState.create({
        doc: documentText,
        extensions: getLanguageExtensions(language),
      }),
      parent,
    });
  };

  beforeEach(() => {
    parent = document.createElement('div');
    document.body.appendChild(parent);
  });

  afterEach(() => {
    view?.destroy();
    parent.remove();
  });

  it('does not decorate plain text', () => {
    createView('{{name}} [person]Ada[/person]', 'plain');

    expect(parent.querySelector('[class*="highlight"]')).toBeNull();
  });

  it('highlights prompt variables', () => {
    createView('Hello {{name}} and {{ title }}.', 'prompt');

    expect(
      Array.from(parent.querySelectorAll('.cm-variable-highlight')).map(
        (element) => element.textContent,
      ),
    ).toEqual(['{{name}}', '{{ title }}']);
  });

  it('updates prompt highlighting after document changes', () => {
    createView('No variable yet', 'prompt');
    expect(parent.querySelector('.cm-variable-highlight')).toBeNull();

    view!.dispatch({
      changes: { from: 0, to: view!.state.doc.length, insert: '{{newValue}}' },
    });

    expect(parent.querySelector('.cm-variable-highlight')?.textContent).toBe(
      '{{newValue}}',
    );
  });

  it('highlights compendium blocks across lines and their tags', () => {
    createView('[person]\nAda Lovelace\n[/person]', 'compendium');

    const blockText = Array.from(
      parent.querySelectorAll('.cm-block-highlight'),
    )
      .map((element) => element.textContent)
      .join('\n');
    expect(blockText).toContain('Ada Lovelace');
    expect(
      Array.from(parent.querySelectorAll('.cm-tag-highlight')).map(
        (element) => element.textContent,
      ),
    ).toEqual(['[person]', '[/person]']);
  });

  it('does not create a block decoration for mismatched tags', () => {
    createView('[person]Ada[/location]', 'compendium');

    expect(parent.querySelector('.cm-block-highlight')).toBeNull();
    expect(parent.querySelectorAll('.cm-tag-highlight').length).toBe(2);
  });
});
