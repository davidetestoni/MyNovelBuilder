import { TestBed } from '@angular/core/testing';
import { ToastrService } from 'ngx-toastr';
import { DynamicDialogRef } from 'primeng/dynamicdialog';
import { of } from 'rxjs';
import { PromptService } from '../../services/prompt.service';
import type { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { PromptType } from '../../types/enums/prompt-type';
import { CreatePromptComponent } from './create-prompt.component';

describe('CreatePromptComponent workflow', () => {
  let component: CreatePromptComponent;
  let promptService: jasmine.SpyObj<PromptService>;
  let toastrService: jasmine.SpyObj<ToastrService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;

  beforeEach(() => {
    promptService = jasmine.createSpyObj<PromptService>('PromptService', [
      'createPrompt',
    ]);
    toastrService = jasmine.createSpyObj<ToastrService>('ToastrService', [
      'success',
    ]);
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>('DynamicDialogRef', [
      'close',
    ]);

    TestBed.configureTestingModule({
      providers: [
        { provide: PromptService, useValue: promptService },
        { provide: ToastrService, useValue: toastrService },
        { provide: DynamicDialogRef, useValue: dialogRef },
      ],
    });

    component = TestBed.runInInjectionContext(
      () => new CreatePromptComponent(),
    );
  });

  it('starts with valid type options and a required name', () => {
    expect(component.promptTypes).toEqual(Object.values(PromptType));
    expect(component.formGroup.controls.type.value).toBe(
      PromptType.GenerateText,
    );
    expect(component.formGroup.controls.name.hasError('required')).toBeTrue();
  });

  it('creates a prompt, reports success, and closes with the result', () => {
    const created = {
      id: 'created',
      createdAt: '2025-01-01T00:00:00Z',
      updatedAt: '2025-01-01T00:00:00Z',
      name: 'Translation prompt',
      type: PromptType.TranslateNovel,
      messages: [],
    } satisfies PromptDto;
    promptService.createPrompt.and.returnValue(of(created));
    component.formGroup.setValue({
      name: 'Translation prompt',
      type: PromptType.TranslateNovel,
    });

    component.createPrompt();

    expect(promptService.createPrompt).toHaveBeenCalledOnceWith({
      name: 'Translation prompt',
      type: PromptType.TranslateNovel,
      messages: [],
    });
    expect(toastrService.success).toHaveBeenCalledOnceWith(
      'Prompt created successfully!',
    );
    expect(dialogRef.close).toHaveBeenCalledOnceWith(created);
  });
});
