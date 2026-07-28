import { TestBed } from '@angular/core/testing';
import { ConfirmationService } from 'primeng/api';
import { PromptService } from '../../services/prompt.service';
import type { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { PromptMessageRole } from '../../types/enums/prompt-message-role';
import { PromptType } from '../../types/enums/prompt-type';
import { PromptComponent } from './prompt.component';

describe('PromptComponent editing', () => {
  let component: PromptComponent;
  let confirmationService: jasmine.SpyObj<ConfirmationService>;

  const prompt = (): PromptDto => ({
    id: 'prompt-id',
    createdAt: '2025-01-01T00:00:00Z',
    updatedAt: '2025-01-01T00:00:00Z',
    name: 'Prompt',
    type: PromptType.GenerateText,
    messages: [
      {
        id: 2,
        role: PromptMessageRole.System,
        message: 'System message',
      },
      {
        id: 8,
        role: PromptMessageRole.User,
        message: 'User message',
      },
    ],
  });

  beforeEach(() => {
    const promptService = jasmine.createSpyObj<PromptService>(
      'PromptService',
      ['getPrompts'],
    );
    confirmationService = jasmine.createSpyObj<ConfirmationService>(
      'ConfirmationService',
      ['confirm'],
    );

    TestBed.configureTestingModule({
      providers: [
        { provide: PromptService, useValue: promptService },
        { provide: ConfirmationService, useValue: confirmationService },
      ],
    });

    component = TestBed.runInInjectionContext(() => new PromptComponent());
    component.prompt = prompt();
  });

  it('emits the prompt when editing loses focus', () => {
    spyOn(component.updatePrompt, 'emit');

    component.onBlur();

    expect(component.updatePrompt.emit).toHaveBeenCalledOnceWith(
      component.prompt,
    );
  });

  it('adds a message with a unique ID and emits the update', () => {
    spyOn(component.updatePrompt, 'emit');

    component.addMessage(PromptMessageRole.Assistant);

    expect(component.prompt.messages[2]).toEqual({
      id: 9,
      role: PromptMessageRole.Assistant,
      message: '',
    });
    expect(component.updatePrompt.emit).toHaveBeenCalledOnceWith(
      component.prompt,
    );
  });

  it('removes only the requested message and emits the update', () => {
    spyOn(component.updatePrompt, 'emit');
    const removed = component.prompt.messages[0];
    const retained = component.prompt.messages[1];

    component.removeMessage(removed);

    expect(component.prompt.messages).toEqual([retained]);
    expect(component.updatePrompt.emit).toHaveBeenCalledOnceWith(
      component.prompt,
    );
  });

  it('emits prompt deletion only after confirmation', () => {
    spyOn(component.deletePrompt, 'emit');

    component.removePrompt();

    expect(confirmationService.confirm).toHaveBeenCalledOnceWith(
      jasmine.objectContaining({
        message:
          'Are you sure you want to delete this prompt? You cannot undo this action.',
        header: 'Confirm Delete',
        icon: 'pi pi-exclamation-triangle',
        acceptButtonStyleClass: 'p-button-danger',
        accept: jasmine.any(Function),
      }),
    );
    expect(component.deletePrompt.emit).not.toHaveBeenCalled();

    confirmationService.confirm.calls.mostRecent().args[0].accept!();

    expect(component.deletePrompt.emit).toHaveBeenCalledOnceWith(
      component.prompt,
    );
  });

  it('identifies prompt types that use novel metadata', () => {
    expect(component.isNovelPrompt(PromptType.GenerateText)).toBeTrue();
    expect(component.isNovelPrompt(PromptType.TranslateNovel)).toBeTrue();
    expect(component.isNovelPrompt(PromptType.PrepareImmersiveTts)).toBeTrue();
    expect(component.isNovelPrompt(PromptType.DescribeImage)).toBeFalse();
    expect(component.isNovelPrompt(PromptType.WorldBuildingAgent)).toBeFalse();
  });

  it('defines keyword help for every supported prompt type', () => {
    for (const promptType of component.promptTypes) {
      expect(component.keywordsByPromptType[promptType])
        .withContext(promptType)
        .toBeDefined();
    }
  });
});
