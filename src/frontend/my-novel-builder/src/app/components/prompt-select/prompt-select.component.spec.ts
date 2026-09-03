import { SimpleChange } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { LocalStorageService } from '../../services/local-storage.service';
import { PromptService } from '../../services/prompt.service';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { PromptType } from '../../types/enums/prompt-type';
import { PromptSelectComponent } from './prompt-select.component';

describe('PromptSelectComponent', () => {
  let component: PromptSelectComponent;
  let promptService: jasmine.SpyObj<PromptService>;
  let localStorageService: jasmine.SpyObj<LocalStorageService>;

  const prompt = (
    id: string,
    type = PromptType.GenerateText,
  ): PromptDto => ({
    id,
    createdAt: '2026-07-30T10:00:00Z',
    updatedAt: '2026-07-30T10:00:00Z',
    name: `Prompt ${id}`,
    type,
    messages: [],
  });

  beforeEach(() => {
    promptService = jasmine.createSpyObj<PromptService>('PromptService', [
      'getPrompts',
    ]);
    localStorageService = jasmine.createSpyObj<LocalStorageService>(
      'LocalStorageService',
      ['getNestedStringForKey', 'setNestedStringForKey'],
    );
    promptService.getPrompts.and.returnValue(of([]));
    localStorageService.getNestedStringForKey.and.returnValue(null);

    TestBed.configureTestingModule({
      providers: [
        { provide: PromptService, useValue: promptService },
        { provide: LocalStorageService, useValue: localStorageService },
      ],
    });

    component = TestBed.runInInjectionContext(() => new PromptSelectComponent());
  });

  it('uses supplied prompts without requesting them and filters by type', () => {
    component.promptType = PromptType.TranslateNovel;
    component.prompts = [
      prompt('generate'),
      prompt('translate', PromptType.TranslateNovel),
    ];

    component.ngOnInit();

    expect(promptService.getPrompts).not.toHaveBeenCalled();
    expect(component.options.map((option) => option.id)).toEqual(['translate']);
    expect(component.value).toBe('translate');
  });

  it('loads and filters prompts from the service', () => {
    promptService.getPrompts.and.returnValue(
      of([
        prompt('translate', PromptType.TranslateNovel),
        prompt('story', PromptType.CreateStoryEvents),
      ]),
    );
    component.promptType = PromptType.CreateStoryEvents;

    component.ngOnInit();

    expect(promptService.getPrompts).toHaveBeenCalledTimes(1);
    expect(component.options.map((option) => option.id)).toEqual(['story']);
    expect(component.value).toBe('story');
    expect(component.isLoading).toBeFalse();
  });

  it('keeps all prompt types when no filter is supplied', () => {
    component.prompts = [
      prompt('generate'),
      prompt('translate', PromptType.TranslateNovel),
    ];

    component.ngOnInit();

    expect(component.options.map((option) => option.id)).toEqual([
      'generate',
      'translate',
    ]);
  });

  it('restores the recent prompt for the active type', () => {
    localStorageService.getNestedStringForKey.and.returnValue('second');
    component.promptType = PromptType.TranslateNovel;
    component.prompts = [
      prompt('first', PromptType.TranslateNovel),
      prompt('second', PromptType.TranslateNovel),
    ];

    component.ngOnInit();

    expect(localStorageService.getNestedStringForKey).toHaveBeenCalledOnceWith(
      LocalStorageKey.RecentPrompts,
      PromptType.TranslateNovel,
    );
    expect(component.value).toBe('second');
  });

  it('uses the explicit storage type independently of the filter type', () => {
    localStorageService.getNestedStringForKey.and.returnValue('second');
    component.promptType = PromptType.TranslateNovel;
    component.storagePromptType = PromptType.GenerateText;
    component.prompts = [
      prompt('first', PromptType.TranslateNovel),
      prompt('second', PromptType.TranslateNovel),
    ];

    component.ngOnInit();

    expect(localStorageService.getNestedStringForKey).toHaveBeenCalledOnceWith(
      LocalStorageKey.RecentPrompts,
      PromptType.GenerateText,
    );
    expect(component.value).toBe('second');
  });

  it('notifies consumers about filtered option counts', () => {
    const optionsChanged = spyOn(component.optionsChanged, 'emit');
    component.promptType = PromptType.TranslateNovel;
    component.prompts = [
      prompt('first', PromptType.TranslateNovel),
      prompt('other'),
      prompt('second', PromptType.TranslateNovel),
    ];

    component.ngOnInit();

    expect(optionsChanged).toHaveBeenCalledOnceWith(2);
  });

  it('keeps a valid written value and replaces an unavailable one', () => {
    component.prompts = [prompt('first'), prompt('second')];
    component.writeValue('second');
    component.ngOnInit();
    expect(component.value).toBe('second');

    component.writeValue('missing');
    expect(component.value).toBe('first');
  });

  it('normalizes written values and defaults blank values', () => {
    component.prompts = [prompt('first'), prompt('second')];
    component.ngOnInit();

    component.writeValue('  second  ');
    expect(component.value).toBe('second');

    component.writeValue('   ');
    expect(component.value).toBe('second');
  });

  it('persists normalized selections and notifies form callbacks', () => {
    const onChange = jasmine.createSpy('onChange');
    const onTouched = jasmine.createSpy('onTouched');
    component.promptType = PromptType.TranslateNovel;
    component.registerOnChange(onChange);
    component.registerOnTouched(onTouched);
    onChange.calls.reset();

    component.onValueChange('  selected  ');

    expect(component.value).toBe('selected');
    expect(onChange).toHaveBeenCalledOnceWith('selected');
    expect(onTouched).toHaveBeenCalledTimes(1);
    expect(
      localStorageService.setNestedStringForKey,
    ).toHaveBeenCalledOnceWith(
      LocalStorageKey.RecentPrompts,
      PromptType.TranslateNovel,
      'selected',
    );
  });

  it('clears whitespace-only selections without persisting them', () => {
    const onChange = jasmine.createSpy('onChange');
    component.registerOnChange(onChange);
    onChange.calls.reset();

    component.onValueChange('   ');

    expect(component.value).toBeNull();
    expect(onChange).toHaveBeenCalledOnceWith(null);
    expect(
      localStorageService.setNestedStringForKey,
    ).not.toHaveBeenCalled();
  });

  it('combines input, form, and loading disabled states', () => {
    expect(component.isDisabled).toBeFalse();

    component.disabled = true;
    expect(component.isDisabled).toBeTrue();
    component.disabled = false;

    component.setDisabledState(true);
    expect(component.isDisabled).toBeTrue();
    component.setDisabledState(false);

    const response = new Subject<PromptDto[]>();
    promptService.getPrompts.and.returnValue(response);
    component.ngOnInit();
    expect(component.isDisabled).toBeTrue();

    response.next([]);
    response.complete();
    expect(component.isDisabled).toBeFalse();
  });

  it('clears stale options and loading state when loading fails', () => {
    const onChange = jasmine.createSpy('onChange');
    const optionsChanged = spyOn(component.optionsChanged, 'emit');
    component.options = [prompt('stale')];
    component.value = 'stale';
    component.registerOnChange(onChange);
    onChange.calls.reset();
    promptService.getPrompts.and.returnValue(
      throwError(() => new Error('request failed')),
    );

    component.ngOnInit();

    expect(component.options).toEqual([]);
    expect(component.value).toBeNull();
    expect(component.isLoading).toBeFalse();
    expect(optionsChanged).toHaveBeenCalledOnceWith(0);
    expect(onChange).toHaveBeenCalledOnceWith(null);
  });

  it('refreshes options when supplied prompts change', () => {
    component.promptType = PromptType.TranslateNovel;
    component.prompts = [prompt('first', PromptType.TranslateNovel)];
    component.ngOnInit();

    component.prompts = [prompt('second', PromptType.TranslateNovel)];
    component.ngOnChanges({
      prompts: new SimpleChange(null, component.prompts, false),
    });

    expect(component.options.map((option) => option.id)).toEqual(['second']);
    expect(component.value).toBe('second');
  });

  it('re-filters supplied options when the prompt type changes', () => {
    component.prompts = [
      prompt('generate'),
      prompt('translate', PromptType.TranslateNovel),
    ];
    component.promptType = PromptType.GenerateText;
    component.ngOnInit();

    component.promptType = PromptType.TranslateNovel;
    component.ngOnChanges({
      promptType: new SimpleChange(
        PromptType.GenerateText,
        PromptType.TranslateNovel,
        false,
      ),
    });

    expect(component.options.map((option) => option.id)).toEqual(['translate']);
    expect(component.value).toBe('translate');
  });

  it('cancels an outstanding service load when supplied prompts arrive', () => {
    const response = new Subject<PromptDto[]>();
    promptService.getPrompts.and.returnValue(response);
    component.ngOnInit();
    expect(component.isLoading).toBeTrue();

    component.prompts = [prompt('supplied')];
    component.ngOnChanges({
      prompts: new SimpleChange(null, component.prompts, false),
    });
    response.next([prompt('late')]);
    response.complete();

    expect(component.isLoading).toBeFalse();
    expect(component.options.map((option) => option.id)).toEqual(['supplied']);
  });

  it('ignores a late service result after destruction', () => {
    const response = new Subject<PromptDto[]>();
    promptService.getPrompts.and.returnValue(response);
    component.ngOnInit();

    component.ngOnDestroy();
    response.next([prompt('late')]);
    response.complete();

    expect(component.options).toEqual([]);
    expect(component.value).toBeNull();
    expect(component.isLoading).toBeFalse();
  });
});
