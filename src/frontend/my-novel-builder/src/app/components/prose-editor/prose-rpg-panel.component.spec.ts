import {
  ProseRpgCommand,
  ProseRpgPanelComponent,
} from './prose-rpg-panel.component';

describe('ProseRpgPanelComponent', () => {
  let component: ProseRpgPanelComponent;

  beforeEach(() => {
    component = new ProseRpgPanelComponent();
  });

  it('derives availability and placeholder state from the selected chapter', () => {
    expect(component.isInputDisabled()).toBeFalse();
    expect(component.getInputPlaceholder()).toBe('Guide the next beat...');

    component.isLastChapterSelected = false;

    expect(component.isInputDisabled()).toBeTrue();
    expect(component.getInputPlaceholder()).toBe(
      'Go to the last chapter for RPG mode',
    );

    component.input = 'Keep this';
    component.visible = false;
    expect(component.isHidden).toBeTrue();
    component.visible = true;
    expect(component.isHidden).toBeFalse();
    expect(component.input).toBe('Keep this');
  });

  it('disables submission until prompt, model, and input are available', () => {
    expect(component.isSendDisabled()).toBeTrue();

    component.promptCount = 1;
    component.selectedPromptId = 'prompt-1';
    component.selectedModel = 'model-1';
    component.input = ' guide ';
    expect(component.isSendDisabled()).toBeFalse();

    component.promptCount = 0;
    expect(component.isSendDisabled()).toBeTrue();

    component.promptCount = 1;
    component.isGenerating = true;
    expect(component.isSendDisabled()).toBeTrue();
  });

  it('updates the action and available prompt count', () => {
    component.setAction('say');
    component.onPromptOptionsChanged(3);

    expect(component.action).toBe('say');
    expect(component.promptCount).toBe(3);
  });

  it('emits a typed command with normalized input', () => {
    component.action = 'say';
    component.input = '  Hello there  ';
    component.selectedPromptId = 'prompt-1';
    component.selectedModel = 'model-1';
    component.promptCount = 1;
    let command: ProseRpgCommand | undefined;
    component.promptSubmitted.subscribe((value) => (command = value));

    component.submitPrompt();

    expect(command).toEqual({
      action: 'say',
      input: 'Hello there',
      promptId: 'prompt-1',
      model: 'model-1',
    });
  });

  it('prevents enter submission defaults and supports input clearing and restoration', () => {
    component.input = 'Continue';
    component.selectedPromptId = 'prompt-1';
    component.selectedModel = 'model-1';
    component.promptCount = 1;
    const emit = spyOn(component.promptSubmitted, 'emit');
    const event = new KeyboardEvent('keydown', { key: 'Enter' });
    spyOn(event, 'preventDefault');

    component.onEnter(event);
    component.clearInput();

    expect(event.preventDefault).toHaveBeenCalledTimes(1);
    expect(emit).toHaveBeenCalledTimes(1);
    expect(component.input).toBe('');

    component.restoreInput('Continue');
    expect(component.input).toBe('Continue');
  });
});
