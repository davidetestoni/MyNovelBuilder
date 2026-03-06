import { DatePipe } from '@angular/common';
import {
  Component,
  EventEmitter,
  HostListener,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
  inject,
} from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { ConfirmationService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { catchError, forkJoin, map, of, Subscription } from 'rxjs';
import * as ExifReader from 'exifreader';
import { EditImageComponent } from '../edit-image/edit-image.component';
import { FileSizePipe } from '../../pipes/file-size.pipe';
import { MediaLibraryService } from '../../services/media-library.service';
import { MediaFileDto } from '../../types/dtos/media-library/media-file.dto';
import { MediaFolderDto } from '../../types/dtos/media-library/media-folder.dto';

interface MediaPreview extends MediaFileDto {
  url: string | null;
  isVideo: boolean;
}

@Component({
  selector: 'app-media-folder',
  standalone: true,
  imports: [ButtonModule, DatePipe, FileSizePipe],
  templateUrl: './media-folder.component.html',
  styleUrl: './media-folder.component.scss',
})
export class MediaFolderComponent implements OnChanges, OnDestroy {
  private mediaLibraryService = inject(MediaLibraryService);
  private toastrService = inject(ToastrService);
  private confirmationService = inject(ConfirmationService);
  private dialogService = inject(DialogService);
  private previewLoadSubscription: Subscription | null = null;
  private dialogRef: DynamicDialogRef | null = null;
  private previewRequestId = 0;
  private zoomedPromptRequestId = 0;
  private currentFolderId: string | null = null;
  private mediaPreviewByFileName = new Map<string, MediaPreview>();

  private _folder: MediaFolderDto | null = null;
  private _mediaFiles: MediaFileDto[] | null = null;

  @Input()
  set folder(value: MediaFolderDto | null | undefined) {
    this._folder = value ?? null;
  }

  get folder(): MediaFolderDto | null {
    return this._folder;
  }

  @Input()
  set mediaFiles(value: MediaFileDto[] | null | undefined) {
    this._mediaFiles = value ?? null;
  }

  get mediaFiles(): MediaFileDto[] | null {
    return this._mediaFiles;
  }

  @Input() uploadingMedia = false;
  @Input() isLoadingMoreMedia = false;
  @Input() hasMoreMedia = false;

  @Output() addMedia = new EventEmitter<void>();
  @Output() deleteMedia = new EventEmitter<string>();
  @Output() replaceMedia = new EventEmitter<{ name: string; file: File }>();

  mediaPreviews: MediaPreview[] | null = null;
  zoomedMedia: MediaPreview | null = null;
  zoomedMediaPrompt: string | null = null;
  isZoomedMediaPromptLoading = false;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['folder'] || changes['mediaFiles']) {
      this.loadMediaPreviews();
    }
  }

  ngOnDestroy(): void {
    this.previewLoadSubscription?.unsubscribe();
    this.dialogRef?.close();
    this.clearPreviewCache();
  }

  confirmDeleteMedia(fileName: string): void {
    this.confirmationService.confirm({
      message: `Delete "${fileName}"? This action cannot be undone.`,
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.deleteMedia.emit(fileName);
      },
    });
  }

  editImage(media: MediaPreview): void {
    const folder = this.folder;
    if (folder === null || media.isVideo) {
      return;
    }

    this.mediaLibraryService.getMediaBlob(folder.id, media.fileName).subscribe({
      next: (blob) => {
        const file = new File([blob], media.fileName, {
          type: blob.type || 'image/png',
        });

        this.dialogRef = this.dialogService.open(EditImageComponent, {
          header: 'Edit Image',
          width: '70vw',
          contentStyle: { overflow: 'auto' },
          baseZIndex: 10000,
          closable: true,
          closeOnEscape: true,
          modal: true,
          dismissableMask: true,
          data: {
            image: file,
          },
        });

        this.dialogRef?.onClose.subscribe((editedImage: Blob | undefined) => {
          if (!editedImage) {
            return;
          }

          this.replaceMedia.emit({
            name: media.fileName,
            file: new File([editedImage], media.fileName, {
              type: editedImage.type || 'image/png',
            }),
          });
        });
      },
    });
  }

  zoomMedia(media: MediaPreview): void {
    if (media.url === null) {
      return;
    }

    this.zoomedMedia = media;
    void this.loadZoomedMediaPrompt(media);
  }

  unzoomMedia(): void {
    this.zoomedMedia = null;
    this.zoomedMediaPrompt = null;
    this.isZoomedMediaPromptLoading = false;
  }

  @HostListener('document:keydown', ['$event'])
  onDocumentKeyDown(event: KeyboardEvent): void {
    if (this.zoomedMedia === null) {
      return;
    }

    if (event.key === 'ArrowLeft') {
      event.preventDefault();
      this.showPreviousMedia();
    } else if (event.key === 'ArrowRight') {
      event.preventDefault();
      this.showNextMedia();
    } else if (event.key === 'Escape') {
      event.preventDefault();
      this.unzoomMedia();
    }
  }

  private loadMediaPreviews(): void {
    this.previewLoadSubscription?.unsubscribe();
    this.previewLoadSubscription = null;

    if (this.folder?.id !== this.currentFolderId) {
      this.clearPreviewCache();
      this.currentFolderId = this.folder?.id ?? null;
    }

    if (this.folder == null || this.mediaFiles == null) {
      this.mediaPreviews = this.mediaFiles == null ? null : [];
      return;
    }

    if (this.mediaFiles.length === 0) {
      this.clearPreviewCache();
      this.mediaPreviews = [];
      return;
    }

    const nextFileNames = new Set(this.mediaFiles.map((file) => file.fileName));
    for (const [fileName, preview] of this.mediaPreviewByFileName.entries()) {
      if (!nextFileNames.has(fileName)) {
        if (preview.url !== null) {
          URL.revokeObjectURL(preview.url);
        }

        this.mediaPreviewByFileName.delete(fileName);
      }
    }

    const filesToLoad: MediaFileDto[] = [];
    for (const file of this.mediaFiles) {
      const existingPreview = this.mediaPreviewByFileName.get(file.fileName);
      const isUnchanged =
        existingPreview &&
        existingPreview.lastModifiedAt === file.lastModifiedAt &&
        existingPreview.sizeBytes === file.sizeBytes;

      if (isUnchanged) {
        continue;
      }

      if (existingPreview && existingPreview.url !== null) {
        URL.revokeObjectURL(existingPreview.url);
      }

      this.mediaPreviewByFileName.set(file.fileName, {
        ...file,
        url: null,
        isVideo: this.isVideoFile(file.fileName),
      });
      filesToLoad.push(file);
    }

    this.syncOrderedPreviews();

    if (filesToLoad.length === 0) {
      return;
    }

    const requestId = ++this.previewRequestId;

    this.previewLoadSubscription = forkJoin(
      filesToLoad.map((file) =>
        this.mediaLibraryService.getMediaBlob(this.folder!.id, file.fileName).pipe(
          map((blob) => ({
            ...file,
            url: URL.createObjectURL(blob),
            isVideo: this.isVideoFile(file.fileName, blob.type),
          })),
          catchError(() =>
            of({
              ...file,
              url: null,
              isVideo: this.isVideoFile(file.fileName),
            }),
          ),
        ),
      ),
    ).subscribe((previews) => {
      if (requestId !== this.previewRequestId) {
        previews.forEach((preview) => {
          if (preview.url !== null) {
            URL.revokeObjectURL(preview.url);
          }
        });
        return;
      }

      for (const preview of previews) {
        this.mediaPreviewByFileName.set(preview.fileName, preview);
      }

      this.syncOrderedPreviews();
    });
  }

  private syncOrderedPreviews(): void {
    this.mediaPreviews = (this.mediaFiles ?? [])
      .map((file) => this.mediaPreviewByFileName.get(file.fileName))
      .filter((preview): preview is MediaPreview => preview !== undefined);
  }

  private showPreviousMedia(): void {
    if (this.mediaPreviews === null || this.mediaPreviews.length === 0 || this.zoomedMedia === null) {
      return;
    }

    const currentIndex = this.mediaPreviews.findIndex(
      (media) => media.fileName === this.zoomedMedia?.fileName,
    );

    if (currentIndex === -1) {
      return;
    }

    const previousIndex =
      (currentIndex - 1 + this.mediaPreviews.length) % this.mediaPreviews.length;
    this.zoomedMedia = this.mediaPreviews[previousIndex];
  }

  private showNextMedia(): void {
    if (this.mediaPreviews === null || this.mediaPreviews.length === 0 || this.zoomedMedia === null) {
      return;
    }

    const currentIndex = this.mediaPreviews.findIndex(
      (media) => media.fileName === this.zoomedMedia?.fileName,
    );

    if (currentIndex === -1) {
      return;
    }

    const nextIndex = (currentIndex + 1) % this.mediaPreviews.length;
    this.zoomedMedia = this.mediaPreviews[nextIndex];
  }

  private clearPreviewCache(): void {
    this.zoomedMedia = null;
    this.zoomedMediaPrompt = null;
    this.isZoomedMediaPromptLoading = false;
    for (const preview of this.mediaPreviewByFileName.values()) {
      if (preview.url !== null) {
        URL.revokeObjectURL(preview.url);
      }
    }

    this.mediaPreviewByFileName.clear();
    this.mediaPreviews = this.mediaFiles == null ? null : [];
  }

  private isVideoFile(fileName: string, mimeType?: string): boolean {
    if (mimeType?.startsWith('video/')) {
      return true;
    }

    return /\.(mp4|webm|ogg|mov)$/i.test(fileName);
  }

  copyZoomedMediaPrompt(): void {
    if (!this.zoomedMediaPrompt) {
      return;
    }

    navigator.clipboard.writeText(this.zoomedMediaPrompt).then(
      () => this.toastrService.success('Prompt copied to clipboard'),
      () => this.toastrService.error('Failed to copy prompt'),
    );
  }

  private async loadZoomedMediaPrompt(media: MediaPreview): Promise<void> {
    if (media.isVideo || media.url === null) {
      this.zoomedMediaPrompt = null;
      this.isZoomedMediaPromptLoading = false;
      return;
    }

    const requestId = ++this.zoomedPromptRequestId;
    this.isZoomedMediaPromptLoading = true;
    this.zoomedMediaPrompt = null;

    try {
      const response = await fetch(media.url);
      if (!response.ok) {
        return;
      }

      const imageBuffer = await response.arrayBuffer();
      const prompt = await this.extractPromptFromImageMetadata(imageBuffer);

      if (requestId === this.zoomedPromptRequestId) {
        this.zoomedMediaPrompt = prompt;
      }
    } catch {
      if (requestId === this.zoomedPromptRequestId) {
        this.zoomedMediaPrompt = null;
      }
    } finally {
      if (requestId === this.zoomedPromptRequestId) {
        this.isZoomedMediaPromptLoading = false;
      }
    }
  }

  private async extractPromptFromImageMetadata(
    imageBuffer: ArrayBuffer,
  ): Promise<string | null> {
    try {
      const tags = (await ExifReader.load(imageBuffer, {
        expanded: true,
        async: true,
      })) as ExifReader.ExpandedTags;

      return tags.pngText?.['prompt (en)']?.description || null;
    } catch {
      return null;
    }
  }
}
