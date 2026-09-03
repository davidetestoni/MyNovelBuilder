import { Injectable, OnDestroy, Type, inject } from '@angular/core';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { EMPTY, Observable, take } from 'rxjs';
import {
  GenerateCompendiumRecordComponentData,
  GenerateCompendiumRecordResultComponent,
} from '../generate-compendium-record-result/generate-compendium-record-result.component';
import {
  GenerateStorySuggestionsDialogComponent,
  GenerateStorySuggestionsDialogData,
  GenerateStorySuggestionsDialogResult,
} from '../generate-story-suggestions-dialog/generate-story-suggestions-dialog.component';
import {
  GenerateTextResultComponent,
  GenerateTextResultComponentData,
} from '../generate-text-result/generate-text-result.component';
import {
  GenerateTextComponent,
  GenerateTextComponentData,
} from '../generate-text/generate-text.component';
import {
  RecordOverridesEditorComponent,
  RecordOverridesEditorComponentData,
} from '../record-overrides-editor/record-overrides-editor.component';
import { GenerateTextRequestDto } from '../../types/dtos/generate/generate-text-request.dto';
import { RecordOverride } from '../../types/dtos/novel/prose';

export type ProseGenerationDialogTitle =
  | 'Generate Section Summary'
  | 'Generate Text'
  | 'Replace Text'
  | 'Create Compendium Record';

@Injectable()
export class ProseGenerationDialogService implements OnDestroy {
  private readonly dialogService = inject(DialogService);
  private dialogRef: DynamicDialogRef | null = null;

  ngOnDestroy(): void {
    this.dialogRef?.close();
  }

  openTextRequestDialog(
    header: ProseGenerationDialogTitle,
    data: GenerateTextComponentData,
  ): Observable<GenerateTextRequestDto | undefined> {
    return this.openDialog<GenerateTextRequestDto | undefined>(
      GenerateTextComponent,
      header,
      data,
    );
  }

  openTextResultDialog(
    header: ProseGenerationDialogTitle,
    data: GenerateTextResultComponentData,
  ): Observable<string | 'back' | undefined> {
    return this.openDialog<string | 'back' | undefined>(
      GenerateTextResultComponent,
      header,
      data,
    );
  }

  openStorySuggestionsDialog(
    data: GenerateStorySuggestionsDialogData,
  ): Observable<GenerateStorySuggestionsDialogResult | undefined> {
    return this.openDialog<GenerateStorySuggestionsDialogResult | undefined>(
      GenerateStorySuggestionsDialogComponent,
      'Story Suggestions',
      data,
    );
  }

  openCompendiumRecordResultDialog(
    data: GenerateCompendiumRecordComponentData,
  ): Observable<boolean | undefined> {
    return this.openDialog<boolean | undefined>(
      GenerateCompendiumRecordResultComponent,
      'Create Compendium Record',
      data,
    );
  }

  openRecordOverridesDialog(
    data: RecordOverridesEditorComponentData,
  ): Observable<RecordOverride[] | undefined> {
    return this.openDialog<RecordOverride[] | undefined>(
      RecordOverridesEditorComponent,
      'Record Overrides',
      data,
      false,
    );
  }

  private openDialog<TResult>(
    component: Type<unknown>,
    header: string,
    data: unknown,
    focusOnShow?: boolean,
  ): Observable<TResult> {
    const dialogRef = this.dialogService.open(component, {
      header,
      width: '50vw',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      modal: true,
      closable: true,
      closeOnEscape: true,
      dismissableMask: true,
      ...(focusOnShow === undefined ? {} : { focusOnShow }),
      data,
    });

    if (!dialogRef) {
      return EMPTY;
    }

    this.dialogRef = dialogRef;
    return dialogRef.onClose.pipe(take(1));
  }
}
