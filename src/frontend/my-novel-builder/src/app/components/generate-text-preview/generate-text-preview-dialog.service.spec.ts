import { TestBed } from '@angular/core/testing';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { of } from 'rxjs';
import {
  GenerateTextRequestDto,
  NovelTextGenerationType,
  SendChatMessageContextInfoDto,
} from '../../types/dtos/generate/generate-text-request.dto';
import { GenerateTextPreviewComponent } from './generate-text-preview.component';
import { GenerateTextPreviewDialogService } from './generate-text-preview-dialog.service';

describe('GenerateTextPreviewDialogService', () => {
  let service: GenerateTextPreviewDialogService;
  let dialogService: jasmine.SpyObj<DialogService>;

  const request = (): GenerateTextRequestDto => {
    const contextInfo: SendChatMessageContextInfoDto = {
      $type: NovelTextGenerationType.SendChatMessage,
      novelId: 'novel-1',
      chapterIndex: null,
      userMessage: 'Hello',
      previousMessages: [],
      compendiumIds: [],
      compendiumRecordIds: [],
    };

    return {
      model: 'model-a',
      promptId: 'prompt-a',
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
      () => new GenerateTextPreviewDialogService(),
    );
  });

  it('opens the reusable prompt preview with shared dialog settings', () => {
    const dialogRef = {} as DynamicDialogRef;
    const previewRequest = request();
    dialogService.open.and.returnValue(dialogRef);

    expect(service.open(previewRequest)).toBe(dialogRef);
    expect(dialogService.open).toHaveBeenCalledOnceWith(
      GenerateTextPreviewComponent,
      {
        header: 'Prompt Preview',
        width: '50vw',
        contentStyle: { overflow: 'auto' },
        baseZIndex: 10000,
        modal: true,
        closable: true,
        closeOnEscape: true,
        dismissableMask: true,
        focusOnShow: false,
        data: { request: previewRequest },
      },
    );
  });

  it('opens a labeled batch using one shared modal', () => {
    const first = request();
    const second = request();

    service.openBatch([
      { label: 'First', request: first },
      { label: 'Second', request: second },
    ]);

    expect(dialogService.open).toHaveBeenCalledOnceWith(
      GenerateTextPreviewComponent,
      jasmine.objectContaining({
        data: {
          model: 'model-a',
          items: [
            { label: 'First', request: first },
            { label: 'Second', request: second },
          ],
        },
      }),
    );
  });

  it('opens an externally supplied exact preview', () => {
    const preview$ = of({
      inputTokens: 10,
      includedCompendiumRecordIds: [],
      finalMessages: [],
    });

    service.openPreview('model-a', preview$, 'World Builder');

    expect(dialogService.open).toHaveBeenCalledOnceWith(
      GenerateTextPreviewComponent,
      jasmine.objectContaining({
        data: {
          model: 'model-a',
          items: [{ label: 'World Builder', preview: preview$ }],
        },
      }),
    );
  });
});
