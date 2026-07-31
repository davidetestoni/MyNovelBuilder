import {
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnChanges,
  OnDestroy,
  OnInit,
  Output,
  SimpleChanges,
  ViewChild,
  forwardRef,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import {
  EditorView,
  placeholder,
  highlightSpecialChars,
  drawSelection,
  dropCursor,
  rectangularSelection,
  crosshairCursor,
  keymap,
} from '@codemirror/view';
import { EditorState, Extension, Compartment } from '@codemirror/state';
import {
  indentOnInput,
  syntaxHighlighting,
  defaultHighlightStyle,
  bracketMatching,
  foldKeymap,
} from '@codemirror/language';
import { history, defaultKeymap, historyKeymap } from '@codemirror/commands';
import { highlightSelectionMatches, searchKeymap } from '@codemirror/search';
import {
  autocompletion,
  completionKeymap,
  closeBrackets,
  closeBracketsKeymap,
} from '@codemirror/autocomplete';
import { lintKeymap } from '@codemirror/lint';
import { oneDark } from '@codemirror/theme-one-dark';
import { EditorLanguage, getLanguageExtensions } from './editor-languages';

@Component({
  selector: 'app-code-editor',
  standalone: true,
  templateUrl: './code-editor.component.html',
  styleUrl: './code-editor.component.scss',
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => CodeEditorComponent),
      multi: true,
    },
  ],
})
export class CodeEditorComponent
  implements OnInit, OnChanges, OnDestroy, ControlValueAccessor
{
  @ViewChild('editorContainer', { static: true })
  editorContainer?: ElementRef<HTMLElement>;
  @Input() placeholderText: string = '';
  @Input() hasBorder: boolean = true;
  @Input() language: EditorLanguage = 'plain';
  @Output() blur = new EventEmitter<void>();
  @Output() keydown = new EventEmitter<KeyboardEvent>();

  private view?: EditorView;
  private value: string = '';
  private editableCompartment = new Compartment();
  private placeholderCompartment = new Compartment();
  private languageCompartment = new Compartment();
  private isDisabled = false;

  private onChange: (value: string) => void = () => {};
  private onTouched: () => void = () => {};

  private getExtensions(): Extension[] {
    return [
      highlightSpecialChars(),
      history(),
      drawSelection(),
      dropCursor(),
      EditorState.allowMultipleSelections.of(true),
      indentOnInput(),
      syntaxHighlighting(defaultHighlightStyle, { fallback: true }),
      bracketMatching(),
      closeBrackets(),
      autocompletion(),
      rectangularSelection(),
      crosshairCursor(),
      highlightSelectionMatches(),
      keymap.of([
        ...closeBracketsKeymap,
        ...defaultKeymap,
        ...searchKeymap,
        ...historyKeymap,
        ...foldKeymap,
        ...completionKeymap,
        ...lintKeymap,
      ]),
      this.languageCompartment.of(getLanguageExtensions(this.language)),
      EditorView.lineWrapping,
      this.editableCompartment.of(EditorView.editable.of(!this.isDisabled)),
      this.placeholderCompartment.of(placeholder(this.placeholderText)),
      EditorView.domEventObservers({
        keydown: (event) => {
          this.keydown.emit(event);
        },
      }),
      EditorView.updateListener.of((update) => {
        if (update.docChanged) {
          const nextValue = update.state.doc.toString();
          if (nextValue !== this.value) {
            this.value = nextValue;
            this.onChange(this.value);
          }
        }
        if (update.focusChanged && !update.view.hasFocus) {
          this.onTouched();
          this.blur.emit();
        }
      }),
      oneDark,
      EditorView.theme({
        "&": {
          backgroundColor: "var(--p-surface-800) !important",
        },
        ".cm-gutters": {
          backgroundColor: "var(--p-surface-800) !important",
          border: "none"
        },
        ".cm-content, .cm-scroller, .cm-editor, .cm-content *": { 
          fontFamily: "'JetBrains Mono', monospace !important"
        }
      })
    ];
  }

  ngOnInit(): void {
    if (!this.editorContainer) {
      return;
    }

    const state = EditorState.create({
      doc: this.value,
      extensions: this.getExtensions(),
    });

    this.view = new EditorView({
      state,
      parent: this.editorContainer.nativeElement,
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (this.view) {
      if (changes['placeholderText']) {
        this.view.dispatch({
          effects: this.placeholderCompartment.reconfigure(
            placeholder(this.placeholderText)
          ),
        });
      }
      if (changes['language']) {
        this.view.dispatch({
          effects: this.languageCompartment.reconfigure(
            getLanguageExtensions(this.language)
          ),
        });
      }
    }
  }

  ngOnDestroy(): void {
    if (this.view) {
      this.view.destroy();
    }
  }

  writeValue(value: string | null | undefined): void {
    this.value = value ?? '';
    if (this.view) {
      const currentDoc = this.view.state.doc.toString();
      if (currentDoc !== this.value) {
        this.view.dispatch({
          changes: { from: 0, to: currentDoc.length, insert: this.value },
        });
      }
    }
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState?(isDisabled: boolean): void {
    this.isDisabled = isDisabled;
    if (this.view) {
      this.view.dispatch({
        effects: this.editableCompartment.reconfigure(
          EditorView.editable.of(!isDisabled)
        ),
      });
    }
  }
}
