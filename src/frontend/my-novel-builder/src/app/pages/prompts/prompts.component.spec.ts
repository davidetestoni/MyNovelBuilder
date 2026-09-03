import { TestBed } from '@angular/core/testing';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { of, Subject } from 'rxjs';
import { CreatePromptComponent } from '../../components/create-prompt/create-prompt.component';
import { PromptService } from '../../services/prompt.service';
import type { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { PromptMessageRole } from '../../types/enums/prompt-message-role';
import { PromptType } from '../../types/enums/prompt-type';
import { PromptsComponent } from './prompts.component';

describe('PromptsComponent workflows', () => {
  let component: PromptsComponent;
  let promptService: jasmine.SpyObj<PromptService>;
  let dialogService: jasmine.SpyObj<DialogService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let dialogClosed: Subject<PromptDto | undefined>;

  const prompt = (
    id: string,
    name: string,
    type = PromptType.GenerateText,
  ): PromptDto => ({
    id,
    createdAt: '2025-01-01T00:00:00Z',
    updatedAt: '2025-01-01T00:00:00Z',
    name,
    type,
    messages: [
      {
        id: 0,
        role: PromptMessageRole.User,
        message: 'Message',
      },
    ],
  });

  beforeEach(() => {
    promptService = jasmine.createSpyObj<PromptService>('PromptService', [
      'getPrompts',
      'updatePrompt',
      'deletePrompt',
    ]);
    dialogService = jasmine.createSpyObj<DialogService>('DialogService', [
      'open',
    ]);
    dialogClosed = new Subject<PromptDto | undefined>();
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>(
      'DynamicDialogRef',
      ['close'],
      { onClose: dialogClosed.asObservable() },
    );

    promptService.getPrompts.and.returnValue(of([]));
    promptService.updatePrompt.and.callFake((update) =>
      of({
        ...prompt(update.id, update.name, update.type),
        messages: update.messages,
      }),
    );
    promptService.deletePrompt.and.returnValue(of(undefined));
    dialogService.open.and.returnValue(dialogRef);

    TestBed.configureTestingModule({
      providers: [
        { provide: PromptService, useValue: promptService },
        { provide: DialogService, useValue: dialogService },
      ],
    });

    component = TestBed.runInInjectionContext(() => new PromptsComponent());
  });

  it('loads prompts on initialization', () => {
    const loadedPrompts = [
      prompt('first', 'First'),
      prompt('second', 'Second'),
    ];
    promptService.getPrompts.and.returnValue(of(loadedPrompts));

    component.ngOnInit();

    expect(component.prompts).toBe(loadedPrompts);
    expect(promptService.getPrompts).toHaveBeenCalledTimes(1);
  });

  it('refreshes the current prompt by identity and clears a missing one', () => {
    component.currentPrompt = prompt('selected', 'Stale');
    const refreshed = prompt('selected', 'Refreshed');
    promptService.getPrompts.and.returnValue(of([refreshed]));

    component.getPrompts();

    expect(component.currentPrompt).toBe(refreshed);

    promptService.getPrompts.and.returnValue(of([]));
    component.getPrompts();
    expect(component.currentPrompt).toBeNull();
  });

  it('selects a prompt', () => {
    const selected = prompt('selected', 'Selected');

    component.setCurrentPrompt(selected);

    expect(component.currentPrompt).toBe(selected);
  });

  it('filters prompts by the selected type', () => {
    const generatePrompt = prompt(
      'generate',
      'Generate',
      PromptType.GenerateText,
    );
    const translatePrompt = prompt(
      'translate',
      'Translate',
      PromptType.TranslateNovel,
    );
    component.prompts = [generatePrompt, translatePrompt];

    expect(component.getFilteredPrompts()).toEqual([
      generatePrompt,
      translatePrompt,
    ]);

    component.selectedPromptType = PromptType.TranslateNovel;
    expect(component.getFilteredPrompts()).toEqual([translatePrompt]);

    component.prompts = null;
    expect(component.getFilteredPrompts()).toEqual([]);
  });

  it('provides readable labels for every prompt-type filter', () => {
    expect(component.promptTypeOptions).toHaveSize(
      Object.values(PromptType).length,
    );
    expect(
      component.promptTypeOptions.find(
        ({ value }) => value === PromptType.SendChatMessage,
      ),
    ).toEqual({
      label: 'Send Chat Message',
      value: PromptType.SendChatMessage,
    });
  });

  it('opens the create dialog and refreshes a created prompt', () => {
    const created = prompt('created', 'Created');
    const refreshed = prompt('created', 'Refreshed');
    promptService.getPrompts.and.returnValue(of([refreshed]));

    component.openCreatePromptDialog();

    expect(dialogService.open).toHaveBeenCalledOnceWith(
      CreatePromptComponent,
      jasmine.objectContaining({
        header: 'Create a prompt',
        modal: true,
        closable: true,
      }),
    );

    dialogClosed.next(undefined);
    expect(promptService.getPrompts).not.toHaveBeenCalled();

    dialogClosed.next(created);
    expect(promptService.getPrompts).toHaveBeenCalledTimes(1);
    expect(component.currentPrompt).toBe(refreshed);
  });

  it('closes an open create dialog when destroyed', () => {
    component.openCreatePromptDialog();

    component.ngOnDestroy();

    expect(dialogRef.close).toHaveBeenCalledTimes(1);
  });

  it('persists only editable prompt fields', () => {
    const edited = prompt('prompt-id', 'Edited', PromptType.ReplaceText);

    component.updatePrompt(edited);

    expect(promptService.updatePrompt).toHaveBeenCalledOnceWith({
      id: 'prompt-id',
      name: 'Edited',
      type: PromptType.ReplaceText,
      messages: edited.messages,
    });
  });

  it('deletes a prompt, refreshes the list, and clears selection', () => {
    const target = prompt('target', 'Target');
    component.currentPrompt = target;

    component.deletePrompt(target);

    expect(promptService.deletePrompt).toHaveBeenCalledOnceWith('target');
    expect(promptService.getPrompts).toHaveBeenCalledTimes(1);
    expect(component.currentPrompt).toBeNull();
  });
});
