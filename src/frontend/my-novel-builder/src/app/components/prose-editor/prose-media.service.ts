import { Injectable, OnDestroy, inject } from '@angular/core';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import {
  EMPTY,
  Observable,
  Subscription,
  filter,
  firstValueFrom,
  switchMap,
  take,
} from 'rxjs';
import { NovelService } from '../../services/novel.service';
import { readImageFileFromClipboard } from '../../utils/clipboard-image';
import { createGeneratedMediaFile } from '../../utils/generated-media';
import { GenerateMediaComponent } from '../generate-media/generate-media.component';
import { ImageSourceSelectorComponent } from '../image-source-selector/image-source-selector.component';

export type ProseMediaSource = 'upload' | 'generate' | 'clipboard';

@Injectable()
export class ProseMediaService implements OnDestroy {
  private readonly dialogService = inject(DialogService);
  private readonly novelService = inject(NovelService);
  private dialogRef: DynamicDialogRef | null = null;

  ngOnDestroy(): void {
    this.dialogRef?.close();
  }

  selectSource(): Observable<ProseMediaSource> {
    const dialogRef = this.dialogService.open(ImageSourceSelectorComponent, {
      header: 'Add Image',
      width: '300px',
      modal: true,
      closable: true,
      dismissableMask: true,
    });
    if (!dialogRef) {
      return EMPTY;
    }
    this.dialogRef = dialogRef;

    return dialogRef.onClose.pipe(take(1));
  }

  selectFileAndUpload(novelId: string): Observable<string> {
    return new Observable<string>((subscriber) => {
      const fileInput = document.createElement('input');
      let uploadSubscription: Subscription | undefined;
      let removed = false;

      const removeInput = (): void => {
        if (!removed) {
          fileInput.remove();
          removed = true;
        }
      };

      fileInput.type = 'file';
      fileInput.accept = 'image/*,video/*';
      fileInput.onchange = () => {
        const file = fileInput.files?.[0];
        if (!file) {
          subscriber.complete();
          removeInput();
          return;
        }

        uploadSubscription = this.novelService
          .uploadProseImage(novelId, file)
          .subscribe(subscriber);
        removeInput();
      };
      fileInput.click();

      return () => {
        fileInput.onchange = null;
        uploadSubscription?.unsubscribe();
        removeInput();
      };
    });
  }

  async uploadClipboardImage(novelId: string): Promise<string> {
    const file = await readImageFileFromClipboard();
    return firstValueFrom(this.novelService.uploadProseImage(novelId, file));
  }

  generateAndUpload(novelId: string): Observable<string> {
    const dialogRef = this.dialogService.open(GenerateMediaComponent, {
      header: 'Generate Media',
      width: '50vw',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      closable: true,
      closeOnEscape: true,
      modal: true,
      dismissableMask: true,
    });
    if (!dialogRef) {
      return EMPTY;
    }
    this.dialogRef = dialogRef;

    return dialogRef.onClose.pipe(
      take(1),
      filter((media): media is Blob => media instanceof Blob),
      switchMap((media) =>
        this.novelService.uploadProseImage(
          novelId,
          createGeneratedMediaFile(media),
        ),
      ),
    );
  }

  deleteImage(novelId: string, imageId: string): Observable<void> {
    return this.novelService.deleteProseImage(novelId, imageId);
  }
}
