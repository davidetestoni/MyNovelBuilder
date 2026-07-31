import { ElementRef, SimpleChange } from '@angular/core';
import { EditorView } from '@codemirror/view';
import { fakeAsync, tick } from '@angular/core/testing';
import { CodeEditorComponent } from './code-editor.component';

describe('CodeEditorComponent', () => {
  let component: CodeEditorComponent;
  let container: HTMLDivElement;

  const getView = (): EditorView =>
    (component as unknown as { view: EditorView }).view;

  beforeEach(() => {
    component = new CodeEditorComponent();
    container = document.createElement('div');
    document.body.appendChild(container);
    component.editorContainer = new ElementRef(container);
  });

  afterEach(() => {
    component.ngOnDestroy();
    container.remove();
  });

  it('initializes the editor with a value written beforehand', () => {
    component.writeValue('initial value');

    component.ngOnInit();

    expect(getView().state.doc.toString()).toBe('initial value');
    expect(container.querySelector('.cm-editor')).not.toBeNull();
  });

  it('does nothing when initialized without a container', () => {
    component.editorContainer = undefined;

    expect(() => component.ngOnInit()).not.toThrow();
  });

  it('normalizes nullish form values to an empty document', () => {
    component.writeValue('text');
    component.ngOnInit();

    component.writeValue(undefined);

    expect(getView().state.doc.toString()).toBe('');
  });

  it('updates the document when the form writes a new value', () => {
    component.ngOnInit();

    component.writeValue('replacement');

    expect(getView().state.doc.toString()).toBe('replacement');
  });

  it('does not report programmatic form writes as user changes', () => {
    const onChange = jasmine.createSpy('onChange');
    component.registerOnChange(onChange);
    component.ngOnInit();

    component.writeValue('from the form');

    expect(onChange).not.toHaveBeenCalled();
  });

  it('reports document edits through the registered form callback', () => {
    const onChange = jasmine.createSpy('onChange');
    component.registerOnChange(onChange);
    component.ngOnInit();

    getView().dispatch({ changes: { from: 0, insert: 'typed' } });

    expect(onChange).toHaveBeenCalledOnceWith('typed');
  });

  it('renders and reconfigures its placeholder', () => {
    component.placeholderText = 'Start here';
    component.ngOnInit();
    expect(container.querySelector('.cm-placeholder')?.textContent).toBe(
      'Start here',
    );

    component.placeholderText = 'New placeholder';
    component.ngOnChanges({
      placeholderText: new SimpleChange('Start here', 'New placeholder', false),
    });

    expect(container.querySelector('.cm-placeholder')?.textContent).toBe(
      'New placeholder',
    );
  });

  it('reconfigures syntax highlighting when the language changes', () => {
    component.writeValue('{{character}}');
    component.ngOnInit();
    expect(container.querySelector('.cm-variable-highlight')).toBeNull();

    component.language = 'prompt';
    component.ngOnChanges({
      language: new SimpleChange('plain', 'prompt', false),
    });

    expect(container.querySelector('.cm-variable-highlight')?.textContent).toBe(
      '{{character}}',
    );
  });

  it('retains a disabled state set before initialization', () => {
    component.setDisabledState!(true);

    component.ngOnInit();

    expect(getView().contentDOM.contentEditable).toBe('false');
  });

  it('reconfigures editability after initialization', () => {
    component.ngOnInit();

    component.setDisabledState!(true);
    expect(getView().contentDOM.contentEditable).toBe('false');

    component.setDisabledState!(false);
    expect(getView().contentDOM.contentEditable).toBe('true');
  });

  it('forwards keydown events', fakeAsync(() => {
    const emitted = spyOn(component.keydown, 'emit');
    component.ngOnInit();
    getView().focus();
    tick();
    const event = new KeyboardEvent('keydown', {
      key: 'Enter',
      bubbles: true,
    });

    getView().contentDOM.dispatchEvent(event);
    tick();

    expect(emitted).toHaveBeenCalledOnceWith(event);
  }));

  it('marks the control touched and emits when focus leaves the editor', fakeAsync(() => {
    const onTouched = jasmine.createSpy('onTouched');
    const blur = spyOn(component.blur, 'emit');
    const nextControl = document.createElement('button');
    document.body.appendChild(nextControl);
    component.registerOnTouched(onTouched);
    component.ngOnInit();

    getView().focus();
    tick(20);
    nextControl.focus();
    tick(20);

    expect(onTouched).toHaveBeenCalledTimes(1);
    expect(blur).toHaveBeenCalledTimes(1);
    nextControl.remove();
  }));

  it('destroys its CodeMirror view', () => {
    component.ngOnInit();
    const destroy = spyOn(getView(), 'destroy').and.callThrough();

    component.ngOnDestroy();

    expect(destroy).toHaveBeenCalledTimes(1);
  });
});
