import { Extension } from '@codemirror/state';
import {
  Decoration,
  MatchDecorator,
  ViewPlugin,
  ViewUpdate,
  EditorView,
  DecorationSet,
} from '@codemirror/view';

export type EditorLanguage = 'prompt' | 'compendium' | 'plain';

function createRegexPlugin(regexp: RegExp, className: string): Extension {
  const decorator = new MatchDecorator({
    regexp,
    decoration: Decoration.mark({ class: className }),
  });

  return ViewPlugin.fromClass(
    class {
      decorations: DecorationSet;
      constructor(view: EditorView) {
        this.decorations = decorator.createDeco(view);
      }
      update(update: ViewUpdate) {
        this.decorations = decorator.updateDeco(update, this.decorations);
      }
    },
    { decorations: (v) => v.decorations },
  );
}

function createMultiLineHighlightPlugin(regexp: RegExp, className: string): Extension {
  const deco = Decoration.mark({ class: className });

  return ViewPlugin.fromClass(
    class {
      decorations: DecorationSet;
      constructor(view: EditorView) {
        this.decorations = this.getDeco(view);
      }
      update(update: ViewUpdate) {
        if (update.docChanged || update.viewportChanged) {
          this.decorations = this.getDeco(update.view);
        }
      }
      getDeco(view: EditorView) {
        const widgets: any[] = [];
        const text = view.state.doc.toString();
        let match;
        // Reset lastIndex for global regex
        regexp.lastIndex = 0;
        while ((match = regexp.exec(text)) !== null) {
          widgets.push(deco.range(match.index, match.index + match[0].length));
        }
        return Decoration.set(widgets);
      }
    },
    { decorations: (v) => v.decorations },
  );
}

export function getLanguageExtensions(language: EditorLanguage): Extension[] {
  switch (language) {
    case 'prompt':
      return [createRegexPlugin(/\{\{[^}]+\}\}/g, 'cm-variable-highlight')];
    case 'compendium':
      return [
        createMultiLineHighlightPlugin(
          /\[(\w+)\][\s\S]*?\[\/\1\]/g,
          'cm-block-highlight',
        ),
        createRegexPlugin(/\[\/?\w+\]/g, 'cm-tag-highlight'),
      ];
    case 'plain':
    default:
      return [];
  }
}
