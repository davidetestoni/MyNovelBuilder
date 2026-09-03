import { TestBed } from '@angular/core/testing';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { Subject } from 'rxjs';
import { GenerateCompendiumRecordResultComponent } from '../generate-compendium-record-result/generate-compendium-record-result.component';
import { GenerateStorySuggestionsDialogComponent } from '../generate-story-suggestions-dialog/generate-story-suggestions-dialog.component';
import { GenerateTextResultComponent } from '../generate-text-result/generate-text-result.component';
import {
  GenerateTextComponent,
  GenerateTextComponentData,
} from '../generate-text/generate-text.component';
import { RecordOverridesEditorComponent } from '../record-overrides-editor/record-overrides-editor.component';
import {
  GenerateTextContextInfoDto,
  GenerateTextRequestDto,
  NovelTextGenerationType,
} from '../../types/dtos/generate/generate-text-request.dto';
import { ProseGenerationDialogService } from './prose-generation-dialog.service';

describe('ProseGenerationDialogService', () => {
  let service: ProseGenerationDialogService;
  let dialogService: jasmine.SpyObj<DialogService>;

  const createDialogRef = <T>(onClose = new Subject<T>()): DynamicDialogRef =>
    ({
      onClose,
      close: jasmine.createSpy('close'),
    }) as unknown as DynamicDialogRef;

  const createTextDialogData = (): GenerateTextComponentData => {
    const contextInfo: GenerateTextContextInfoDto = {
      $type: NovelTextGenerationType.GenerateText,
      novelId: 'novel-1',
      chapterIndex: 0,
      sectionIndex: 0,
      textOffset: 0,
      instructions: null,
    };

    return {
      prompts: [],
      instructionsRequired: false,
      contextInfo,
    };
  };

  beforeEach(() => {
    dialogService = jasmine.createSpyObj<DialogService>('DialogService', [
      'open',
    ]);

    TestBed.configureTestingModule({
      providers: [{ provide: DialogService, useValue: dialogService }],
    });

    service = TestBed.runInInjectionContext(
      () => new ProseGenerationDialogService(),
    );
  });

  it('opens text requests with the shared dialog configuration', () => {
    const close$ = new Subject<GenerateTextRequestDto>();
    dialogService.open.and.returnValue(createDialogRef(close$));
    const request = { promptId: 'prompt-1' } as GenerateTextRequestDto;
    const received: GenerateTextRequestDto[] = [];
    const data = createTextDialogData();

    service
      .openTextRequestDialog('Generate Section Summary', data)
      .subscribe((value) => value && received.push(value));
    close$.next(request);
    close$.next({ promptId: 'ignored' } as GenerateTextRequestDto);

    expect(dialogService.open).toHaveBeenCalledOnceWith(
      GenerateTextComponent,
      {
        header: 'Generate Section Summary',
        width: '50vw',
        contentStyle: { overflow: 'auto' },
        baseZIndex: 10000,
        modal: true,
        closable: true,
        closeOnEscape: true,
        dismissableMask: true,
        data,
      },
    );
    expect(received).toEqual([request]);
  });

  it('opens generated-text results and returns the selected action', () => {
    const close$ = new Subject<string | 'back' | undefined>();
    dialogService.open.and.returnValue(createDialogRef(close$));
    const data = {
      request: {} as GenerateTextRequestDto,
      textToReplace: 'Selected text',
    };
    let result: string | undefined;

    service
      .openTextResultDialog('Replace Text', data)
      .subscribe((value) => (result = value));
    close$.next('back');

    expect(dialogService.open).toHaveBeenCalledOnceWith(
      GenerateTextResultComponent,
      jasmine.objectContaining({ header: 'Replace Text', data }),
    );
    expect(result).toBe('back');
  });

  it('opens story suggestions with their typed context', () => {
    const close$ = new Subject<{ model: string; instructions: string }>();
    dialogService.open.and.returnValue(createDialogRef(close$));
    const data = {
      prompts: [],
      novelId: 'novel-1',
      chapterIndex: 2,
      sectionIndex: 3,
      textOffset: 42,
    };
    let result: { model: string; instructions: string } | undefined;

    service
      .openStorySuggestionsDialog(data)
      .subscribe((value) => (result = value));
    close$.next({ model: 'model-1', instructions: 'Choose option two' });

    expect(dialogService.open).toHaveBeenCalledOnceWith(
      GenerateStorySuggestionsDialogComponent,
      jasmine.objectContaining({ header: 'Story Suggestions', data }),
    );
    expect(result).toEqual({
      model: 'model-1',
      instructions: 'Choose option two',
    });
  });

  it('opens the generated compendium-record editor', () => {
    const close$ = new Subject<boolean>();
    dialogService.open.and.returnValue(createDialogRef(close$));
    const data = { generatedText: '{"name":"Ayla"}', novelId: 'novel-1' };
    let changed: boolean | undefined;

    service
      .openCompendiumRecordResultDialog(data)
      .subscribe((value) => (changed = value));
    close$.next(true);

    expect(dialogService.open).toHaveBeenCalledOnceWith(
      GenerateCompendiumRecordResultComponent,
      jasmine.objectContaining({
        header: 'Create Compendium Record',
        data,
      }),
    );
    expect(changed).toBeTrue();
  });

  it('disables automatic focus for record overrides', () => {
    const close$ = new Subject<[]>();
    dialogService.open.and.returnValue(createDialogRef(close$));
    const data = {
      recordOverrides: [],
      availableRecords: [],
      prose: { chapters: [] },
      chapterIndex: 0,
      sectionIndex: 0,
    };

    service.openRecordOverridesDialog(data).subscribe();

    expect(dialogService.open).toHaveBeenCalledOnceWith(
      RecordOverridesEditorComponent,
      jasmine.objectContaining({
        header: 'Record Overrides',
        focusOnShow: false,
        data,
      }),
    );
  });

  it('completes when a dialog cannot open', () => {
    dialogService.open.and.returnValue(null);
    let completed = false;

    service
      .openTextRequestDialog('Generate Text', createTextDialogData())
      .subscribe({ complete: () => (completed = true) });

    expect(completed).toBeTrue();
  });

  it('closes the active dialog on destroy', () => {
    const ref = createDialogRef();
    dialogService.open.and.returnValue(ref);

    service.openTextRequestDialog('Generate Text', createTextDialogData());
    service.ngOnDestroy();

    expect(ref.close).toHaveBeenCalledTimes(1);
  });
});
