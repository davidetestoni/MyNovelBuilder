import { DatePipe } from '@angular/common';
import {
  Component,
  ElementRef,
  HostListener,
  Input,
  OnChanges,
  OnDestroy,
  SimpleChanges,
  ViewChild,
  inject,
} from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { ConfirmationService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { PaginatorModule } from 'primeng/paginator';
import type { PaginatorState } from 'primeng/paginator';
import { catchError, forkJoin, map, of, Subscription } from 'rxjs';
import * as ExifReader from 'exifreader';
import { DescribeImageComponent } from '../describe-image/describe-image.component';
import { EditImageComponent } from '../edit-image/edit-image.component';
import {
  ImageSourceSelectorComponent,
  ImageSourceSelectorComponentData,
} from '../image-source-selector/image-source-selector.component';
import { GenerateMediaComponent } from '../generate-media/generate-media.component';
import {
  UploadMediaDialogComponent,
  UploadMediaDialogData,
  UploadMediaDialogResult,
} from '../upload-media-dialog/upload-media-dialog.component';
import { FileSizePipe } from '../../pipes/file-size.pipe';
import { LocalStorageService } from '../../services/local-storage.service';
import { MediaLibraryService } from '../../services/media-library.service';
import { MediaFileDto } from '../../types/dtos/media-library/media-file.dto';
import { MediaFolderDto } from '../../types/dtos/media-library/media-folder.dto';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { PromptType } from '../../types/enums/prompt-type';
import { readImageFileFromClipboard } from '../../utils/clipboard-image';
import { createGeneratedMediaFile } from '../../utils/generated-media';

interface MediaPreview extends MediaFileDto {
  url: string | null;
  isVideo: boolean;
}

@Component({
  selector: 'app-media-folder',
  standalone: true,
  imports: [ButtonModule, DatePipe, FileSizePipe, PaginatorModule],
  templateUrl: './media-folder.component.html',
  styleUrl: './media-folder.component.scss',
})
export class MediaFolderComponent implements OnChanges, OnDestroy {
  private readonly mediaCardMinWidth = 200;
  private readonly mediaGridGap = 12;
  private readonly maxMediaGridRowsPerPage = 5;
  private mediaLibraryService = inject(MediaLibraryService);
  private localStorageService = inject(LocalStorageService);
  private toastrService = inject(ToastrService);
  private confirmationService = inject(ConfirmationService);
  private dialogService = inject(DialogService);
  private previewLoadSubscription: Subscription | null = null;
  private dialogRef: DynamicDialogRef | null = null;
  private resizeObserver: ResizeObserver | null = null;
  private previewRequestId = 0;
  private mediaListRequestId = 0;
  private zoomedPromptRequestId = 0;
  private currentFolderId: string | null = null;
  private currentGridColumns = 1;
  private selectedGridRowsPerPage = 2;
  private mediaPreviewByFileName = new Map<string, MediaPreview>();

  private _folder: MediaFolderDto | null = null;
  private _mediaFiles: MediaFileDto[] | null = null;

  allMediaFiles: MediaFileDto[] = [];

  private mediaFolderContent: ElementRef<HTMLElement> | null = null;

  @Input()
  set folder(value: MediaFolderDto | null | undefined) {
    const nextFolder = value ?? null;
    const previousFolderId = this._folder?.id ?? null;
    this._folder = nextFolder;

    if (nextFolder?.id !== previousFolderId) {
      this.handleFolderChange();
    }
  }

  get folder(): MediaFolderDto | null {
    return this._folder;
  }

  get mediaFiles(): MediaFileDto[] | null {
    return this._mediaFiles;
  }

  @Input() refreshVersion = 0;

  @ViewChild('mediaFolderContent')
  set mediaFolderContentRef(value: ElementRef<HTMLElement> | null | undefined) {
    const nextContent = value ?? null;
    if (this.mediaFolderContent?.nativeElement === nextContent?.nativeElement) {
      return;
    }

    this.resizeObserver?.disconnect();
    this.mediaFolderContent = nextContent;

    const element = nextContent?.nativeElement;
    if (!element) {
      return;
    }

    this.resizeObserver = new ResizeObserver(() => {
      this.updatePageSizeFromLayout();
    });
    this.resizeObserver.observe(element);
    this.updatePageSizeFromLayout();
  }

  uploadingMedia = false;
  currentPageFirst = 0;
  currentPageSize = 8;

  mediaPreviews: MediaPreview[] | null = null;
  zoomedMedia: MediaPreview | null = null;
  zoomedMediaPrompt: string | null = null;
  zoomedMediaDescription: string | null = null;
  isZoomedMediaPromptLoading = false;

  constructor() {
    const storedRowsPerPage = Number(
      this.localStorageService.getStringForKey(LocalStorageKey.MediaFolderRowsPerPage),
    );

    if (Number.isFinite(storedRowsPerPage) && storedRowsPerPage >= 1) {
      this.selectedGridRowsPerPage = Math.min(
        this.maxMediaGridRowsPerPage,
        Math.floor(storedRowsPerPage),
      );
    }
  }

  get showingMediaStart(): number {
    if (this.allMediaFiles.length === 0) {
      return 0;
    }

    return this.currentPageFirst + 1;
  }

  get showingMediaEnd(): number {
    if (this.allMediaFiles.length === 0) {
      return 0;
    }

    return Math.min(this.currentPageFirst + this.currentPageSize, this.allMediaFiles.length);
  }

  get shouldShowPaginator(): boolean {
    return this.allMediaFiles.length > this.currentPageSize;
  }

  get rowsPerPageOptions(): number[] {
    const options = Array.from(
      new Set(
        Array.from({ length: this.maxMediaGridRowsPerPage }, (_, index) =>
          this.currentGridColumns * (index + 1),
        ),
      ),
    );

    return options.sort((left, right) => left - right);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['folder']) {
      this.loadMediaPreviews();
    }

    if (
      changes['refreshVersion'] &&
      !changes['refreshVersion'].firstChange &&
      this.folder !== null
    ) {
      this.loadInitialMedia(this.folder.id);
    }
  }

  ngOnDestroy(): void {
    this.mediaListRequestId += 1;
    this.zoomedPromptRequestId += 1;
    this.previewLoadSubscription?.unsubscribe();
    this.dialogRef?.close();
    this.resizeObserver?.disconnect();
    this.clearPreviewCache();
  }

  onPageChange(event: PaginatorState): void {
    const nextFirst = event.first ?? this.currentPageFirst;
    const nextRows = event.rows ?? this.currentPageSize;

    this.currentPageFirst = nextFirst;
    if (nextRows !== this.currentPageSize) {
      const nextGridRowsPerPage = Math.min(
        this.maxMediaGridRowsPerPage,
        Math.max(1, Math.round(nextRows / this.currentGridColumns)),
      );
      this.selectedGridRowsPerPage = nextGridRowsPerPage;
      this.localStorageService.setStringForKey(
        LocalStorageKey.MediaFolderRowsPerPage,
        String(nextGridRowsPerPage),
      );
      this.currentPageSize = this.currentGridColumns * nextGridRowsPerPage;
    }

    this.updateVisibleMediaFiles();
  }

  confirmDeleteMedia(fileName: string): void {
    this.confirmationService.confirm({
      message: `Delete "${fileName}"? This action cannot be undone.`,
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.deleteMedia(fileName);
      },
    });
  }

  openAddMediaDialog(): void {
    if (this.folder === null) {
      this.toastrService.error('Select a media folder first.');
      return;
    }

    const dialogRef = this.dialogService.open(ImageSourceSelectorComponent, {
      header: 'Add Media',
      width: '300px',
      modal: true,
      closable: true,
      dismissableMask: true,
      closeOnEscape: true,
      data: <ImageSourceSelectorComponentData>{
        uploadLabel: 'Upload File',
        generateLabel: 'Generate Media',
      },
    });

    if (dialogRef === null) {
      return;
    }

    this.dialogRef = dialogRef;

    dialogRef.onClose.subscribe(
      (result: 'upload' | 'generate' | 'clipboard' | undefined) => {
        if (result === 'upload') {
          this.openUploadMediaDialog();
        } else if (result === 'generate') {
          this.openGenerateImageDialog();
        } else if (result === 'clipboard') {
          this.openUploadMediaFromClipboardDialog();
        }
      },
    );
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

          const editedFileName = this.buildEditedFileName(media.fileName);
          this.uploadMedia({
            name: editedFileName,
            file: new File([editedImage], editedFileName, {
              type: editedImage.type || 'image/png',
            }),
          });
        });
      },
      error: () => {
        this.toastrService.error('Failed to load image');
      },
    });
  }

  zoomMedia(media: MediaPreview): void {
    if (media.url === null) {
      return;
    }

    this.setZoomedMedia(media);
  }

  unzoomMedia(): void {
    this.zoomedPromptRequestId += 1;
    this.zoomedMedia = null;
    this.zoomedMediaPrompt = null;
    this.zoomedMediaDescription = null;
    this.isZoomedMediaPromptLoading = false;
  }

  editZoomedMedia(): void {
    const media = this.zoomedMedia;
    if (media === null || media.isVideo) {
      return;
    }

    this.unzoomMedia();
    this.editImage(media);
  }

  deleteZoomedMedia(): void {
    const media = this.zoomedMedia;
    if (media === null) {
      return;
    }

    this.unzoomMedia();
    this.confirmDeleteMedia(media.fileName);
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

  private handleFolderChange(): void {
    const nextFolderId = this.folder?.id ?? null;

    if (nextFolderId === null) {
      this.mediaListRequestId += 1;
      this.currentFolderId = null;
      this.allMediaFiles = [];
      this.currentPageFirst = 0;
      this._mediaFiles = null;
      this.clearPreviewCache();
      return;
    }

    if (nextFolderId === this.currentFolderId) {
      return;
    }

    this.loadInitialMedia(nextFolderId);
  }

  private loadInitialMedia(folderId: string): void {
    const requestId = ++this.mediaListRequestId;
    this.currentFolderId = folderId;
    this._mediaFiles = null;
    this.allMediaFiles = [];
    this.currentPageFirst = 0;
    this.mediaLibraryService.getMedia(folderId).subscribe({
      next: (mediaFiles) => {
        if (
          requestId !== this.mediaListRequestId ||
          this.folder?.id !== folderId
        ) {
          return;
        }

        this.allMediaFiles = mediaFiles;
        this.updateVisibleMediaFiles(true);
        queueMicrotask(() => this.updatePageSizeFromLayout());
      },
      error: () => {
        if (
          requestId !== this.mediaListRequestId ||
          this.folder?.id !== folderId
        ) {
          return;
        }

        this._mediaFiles = [];
        this.loadMediaPreviews();
        this.toastrService.error('Failed to load media.');
      },
    });
  }

  private uploadMedia(request: { name: string; file: File }): void {
    const folderId = this.folder?.id;
    if (folderId === undefined) {
      this.toastrService.error('Select a media folder first.');
      return;
    }

    if (this.uploadingMedia) {
      return;
    }

    if (request.file === null) {
      this.toastrService.error('Choose a file to upload.');
      return;
    }

    const name = request.name.trim();
    if (name === '') {
      this.toastrService.error('Media name is required.');
      return;
    }

    this.uploadingMedia = true;
    this.mediaLibraryService
      .uploadMedia(folderId, name, request.file)
      .subscribe({
        next: () => {
          this.uploadingMedia = false;
          this.toastrService.success('Media uploaded.');
          if (this.folder?.id === folderId) {
            this.loadInitialMedia(folderId);
          }
        },
        error: () => {
          this.uploadingMedia = false;
          this.toastrService.error('Failed to upload media.');
        },
      });
  }

  private deleteMedia(fileName: string): void {
    const folderId = this.folder?.id;
    if (folderId === undefined) {
      return;
    }

    this.mediaLibraryService.deleteMedia(folderId, fileName).subscribe({
      next: () => {
        this.toastrService.success('Media deleted.');
        if (this.folder?.id === folderId) {
          this.loadInitialMedia(folderId);
        }
      },
      error: () => {
        this.toastrService.error('Failed to delete media.');
      },
    });
  }

  private openUploadMediaDialog(data?: UploadMediaDialogData): void {
    const dialogRef = this.dialogService.open(UploadMediaDialogComponent, {
      header: 'Upload Media',
      width: '32rem',
      modal: true,
      closable: true,
      closeOnEscape: true,
      dismissableMask: true,
      contentStyle: { overflow: 'visible' },
      data,
    });

    if (dialogRef === null) {
      return;
    }

    this.dialogRef = dialogRef;

    dialogRef.onClose.subscribe((result: UploadMediaDialogResult | undefined) => {
      if (result) {
        this.uploadMedia(result);
      }
    });
  }

  private openGenerateImageDialog(): void {
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

    if (dialogRef === null) {
      return;
    }

    this.dialogRef = dialogRef;

    dialogRef.onClose.subscribe((media: Blob | undefined) => {
      if (!media) {
        return;
      }

      const file = createGeneratedMediaFile(media);

      this.openUploadMediaDialog({
        initialFile: file,
        initialName: file.name,
      });
    });
  }

  private async openUploadMediaFromClipboardDialog(): Promise<void> {
    try {
      const file = await readImageFileFromClipboard();
      const defaultName = file.name.replace(/\.[^.]+$/, '');
      this.openUploadMediaDialog({
        initialFile: file,
        initialName: defaultName,
      });
    } catch (error) {
      this.toastrService.error(
        error instanceof Error
          ? error.message
          : 'Failed to read image from clipboard.',
      );
    }
  }

  private updatePageSizeFromLayout(): void {
    const panelWidth = this.mediaFolderContent?.nativeElement.clientWidth ?? 0;
    if (panelWidth <= 0) {
      return;
    }

    const columns = Math.max(
      1,
      Math.floor((panelWidth + this.mediaGridGap) / (this.mediaCardMinWidth + this.mediaGridGap)),
    );
    this.currentGridColumns = columns;
    const nextPageSize = columns * this.selectedGridRowsPerPage;

    if (nextPageSize === this.currentPageSize) {
      return;
    }

    const currentPageIndex = Math.floor(this.currentPageFirst / this.currentPageSize);
    this.currentPageSize = nextPageSize;
    this.currentPageFirst = currentPageIndex * nextPageSize;
    this.updateVisibleMediaFiles();
  }

  private updateVisibleMediaFiles(resetPage = false): void {
    if (resetPage) {
      this.currentPageFirst = 0;
    }

    if (this.currentPageFirst >= this.allMediaFiles.length) {
      const lastPageIndex = Math.max(
        0,
        Math.ceil(this.allMediaFiles.length / this.currentPageSize) - 1,
      );
      this.currentPageFirst = lastPageIndex * this.currentPageSize;
    }

    this._mediaFiles = this.allMediaFiles.slice(
      this.currentPageFirst,
      this.currentPageFirst + this.currentPageSize,
    );
    this.loadMediaPreviews();
  }

  private buildEditedFileName(originalFileName: string): string {
    const extensionIndex = originalFileName.lastIndexOf('.');
    const hasExtension = extensionIndex > 0;
    const baseName = hasExtension
      ? originalFileName.slice(0, extensionIndex)
      : originalFileName;
    const extension = hasExtension ? originalFileName.slice(extensionIndex) : '';
    const existingNames = new Set(this.allMediaFiles.map((file) => file.fileName));
    const firstCandidate = `${baseName}-edited${extension}`;

    if (!existingNames.has(firstCandidate)) {
      return firstCandidate;
    }

    let suffix = 2;
    while (existingNames.has(`${baseName}-edited-${suffix}${extension}`)) {
      suffix += 1;
    }

    return `${baseName}-edited-${suffix}${extension}`;
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
    this.setZoomedMedia(this.mediaPreviews[previousIndex]);
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
    this.setZoomedMedia(this.mediaPreviews[nextIndex]);
  }

  private setZoomedMedia(media: MediaPreview): void {
    this.zoomedMedia = media;
    void this.loadZoomedMediaPrompt(media);
  }

  private clearPreviewCache(): void {
    this.zoomedPromptRequestId += 1;
    this.zoomedMedia = null;
    this.zoomedMediaPrompt = null;
    this.zoomedMediaDescription = null;
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
    const text = this.zoomedMediaPrompt ?? this.zoomedMediaDescription;
    if (!text) {
      return;
    }

    navigator.clipboard.writeText(text).then(
      () => this.toastrService.success('Text copied to clipboard'),
      () => this.toastrService.error('Failed to copy text'),
    );
  }

  describeZoomedMedia(): void {
    const media = this.zoomedMedia;
    const folder = this.folder;
    if (media === null || folder === null || media.isVideo) {
      return;
    }

    this.mediaLibraryService.getMediaBlob(folder.id, media.fileName).subscribe({
      next: (blob) => {
        const file = new File([blob], media.fileName, {
          type: blob.type || 'image/png',
        });

        this.dialogRef = this.dialogService.open(DescribeImageComponent, {
          header: 'Describe Image',
          width: '70vw',
          contentStyle: { overflow: 'auto' },
          baseZIndex: 10000,
          closable: true,
          closeOnEscape: true,
          modal: true,
          dismissableMask: true,
          data: {
            image: file,
            promptType: PromptType.DescribeImage,
          },
        });

        this.dialogRef?.onClose.subscribe((description: string | undefined) => {
          if (!description || description.trim() === '') {
            return;
          }

          this.zoomedMediaDescription = description.trim();
        });
      },
      error: () => {
        this.toastrService.error('Failed to load image');
      },
    });
  }

  ensureVideoPlayback(event: Event): void {
    const video = event.target;
    if (!(video instanceof HTMLVideoElement)) {
      return;
    }

    video.defaultMuted = true;
    video.muted = true;
    video.playsInline = true;

    const playPromise = video.play();
    if (playPromise instanceof Promise) {
      playPromise.catch(() => undefined);
    }
  }

  ensureOverlayVideoPlayback(event: Event): void {
    const video = event.target;
    if (!(video instanceof HTMLVideoElement)) {
      return;
    }

    video.defaultMuted = false;
    video.muted = false;
    video.playsInline = true;

    const playPromise = video.play();
    if (playPromise instanceof Promise) {
      playPromise.catch(() => undefined);
    }
  }

  private async loadZoomedMediaPrompt(media: MediaPreview): Promise<void> {
    const requestId = ++this.zoomedPromptRequestId;
    if (media.isVideo || media.url === null) {
      this.zoomedMediaPrompt = null;
      this.zoomedMediaDescription = null;
      this.isZoomedMediaPromptLoading = false;
      return;
    }

    this.isZoomedMediaPromptLoading = true;
    this.zoomedMediaPrompt = null;
    this.zoomedMediaDescription = null;

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
