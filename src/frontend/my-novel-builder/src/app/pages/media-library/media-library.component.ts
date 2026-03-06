import { Component, HostListener, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { ConfirmationService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { SelectModule } from 'primeng/select';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import {
  AddMediaFolderDialogComponent,
  AddMediaFolderDialogResult,
} from '../../components/add-media-folder-dialog/add-media-folder-dialog.component';
import {
  ImageSourceSelectorComponent,
  ImageSourceSelectorComponentData,
} from '../../components/image-source-selector/image-source-selector.component';
import { MediaFolderComponent } from '../../components/media-folder/media-folder.component';
import { GenerateImageComponent } from '../../components/generate-image/generate-image.component';
import {
  UploadMediaDialogComponent,
  UploadMediaDialogData,
  UploadMediaDialogResult,
} from '../../components/upload-media-dialog/upload-media-dialog.component';
import { MediaLibraryService } from '../../services/media-library.service';
import { MediaFileDto } from '../../types/dtos/media-library/media-file.dto';
import { MediaFolderDto } from '../../types/dtos/media-library/media-folder.dto';

@Component({
  selector: 'app-media-library',
  standalone: true,
  templateUrl: './media-library.component.html',
  styleUrl: './media-library.component.scss',
  imports: [
    FormsModule,
    ButtonModule,
    SelectModule,
    ConfirmDialogModule,
    MediaFolderComponent,
  ],
  providers: [DialogService, ConfirmationService],
})
export class MediaLibraryComponent implements OnInit, OnDestroy {
  private readonly mediaPageSize = 24;
  private mediaLibraryService = inject(MediaLibraryService);
  private toastrService = inject(ToastrService);
  private dialogService = inject(DialogService);
  private confirmationService = inject(ConfirmationService);
  private dialogRef: DynamicDialogRef | null = null;
  private allMediaFiles: MediaFileDto[] = [];

  folderOptions: { label: string; value: string; path: string }[] = [];
  folders: MediaFolderDto[] | null = null;
  mediaFiles: MediaFileDto[] | null = null;
  selectedFolderId: string | null = null;
  creatingFolder = false;
  uploadingMedia = false;
  isLoadingMoreMedia = false;
  hasMoreMedia = false;

  get selectedFolder(): MediaFolderDto | null {
    return (
      this.folders?.find((folder) => folder.id === this.selectedFolderId) ?? null
    );
  }

  ngOnInit(): void {
    this.loadFolders();
  }

  ngOnDestroy(): void {
    this.dialogRef?.close();
  }

  @HostListener('window:scroll')
  onWindowScroll(): void {
    this.loadMoreMediaIfNeeded();
  }

  loadFolders(selectFolderId?: string | null): void {
    this.mediaLibraryService.getFolders().subscribe((folders) => {
      this.folders = folders;
      this.folderOptions = folders.map((folder) => ({
        label: folder.name,
        value: folder.id,
        path: folder.path,
      }));

      const nextFolderId =
        selectFolderId && folders.some((folder) => folder.id === selectFolderId)
          ? selectFolderId
          : folders[0]?.id ?? null;

      this.selectedFolderId = nextFolderId;

      if (nextFolderId === null) {
        this.allMediaFiles = [];
        this.mediaFiles = [];
        this.hasMoreMedia = false;
        this.isLoadingMoreMedia = false;
        return;
      }

      this.loadInitialMedia(nextFolderId);
    });
  }

  selectFolder(folderId: string | null): void {
    if (folderId === null) {
      this.selectedFolderId = null;
      this.allMediaFiles = [];
      this.mediaFiles = [];
      this.hasMoreMedia = false;
      this.isLoadingMoreMedia = false;
      return;
    }

    this.selectedFolderId = folderId;
    this.loadInitialMedia(folderId);
  }

  openAddFolderDialog(): void {
    const dialogRef = this.dialogService.open(AddMediaFolderDialogComponent, {
      header: 'Add media folder',
      width: '32rem',
      modal: true,
      closable: true,
      closeOnEscape: true,
      dismissableMask: true,
      contentStyle: { overflow: 'visible' },
    });

    if (dialogRef === null) {
      return;
    }

    this.dialogRef = dialogRef;

    dialogRef.onClose.subscribe((result: AddMediaFolderDialogResult | undefined) => {
      if (!result) {
        return;
      }

      this.createFolder(result);
    });
  }

  createFolder(result: AddMediaFolderDialogResult): void {
    this.creatingFolder = true;
    this.mediaLibraryService.createFolder(result.name, result.path).subscribe({
      next: (folder) => {
        this.creatingFolder = false;
        this.toastrService.success('Media folder linked.');
        this.loadFolders(folder.id);
      },
      error: () => {
        this.creatingFolder = false;
      },
    });
  }

  confirmDeleteSelectedFolder(): void {
    if (this.selectedFolder === null) {
      return;
    }

    this.confirmationService.confirm({
      message: `Unlink "${this.selectedFolder.name}"?`,
      header: 'Confirm Unlink',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.deleteFolder(this.selectedFolder!);
      },
    });
  }

  private deleteFolder(folder: MediaFolderDto): void {
    this.mediaLibraryService.deleteFolder(folder.id).subscribe(() => {
      this.toastrService.success('Media folder unlinked.');
      const nextFolderId =
        this.selectedFolderId === folder.id ? null : this.selectedFolderId;
      this.loadFolders(nextFolderId);
    });
  }

  openAddMediaDialog(): void {
    if (this.selectedFolderId === null) {
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
        generateLabel: 'Generate Image',
      },
    });

    if (dialogRef === null) {
      return;
    }

    this.dialogRef = dialogRef;

    dialogRef.onClose.subscribe((result: 'upload' | 'generate' | undefined) => {
      if (result === 'upload') {
        this.openUploadMediaDialog();
      } else if (result === 'generate') {
        this.openGenerateImageDialog();
      }
    });
  }

  uploadMedia(request: { name: string; file: File }): void {
    if (this.selectedFolderId === null) {
      this.toastrService.error('Select a media folder first.');
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
      .uploadMedia(this.selectedFolderId, name, request.file)
      .subscribe({
        next: () => {
          this.uploadingMedia = false;
          this.toastrService.success('Media uploaded.');
          this.loadInitialMedia(this.selectedFolderId!);
        },
        error: () => {
          this.uploadingMedia = false;
        },
      });
  }

  deleteMedia(fileName: string): void {
    if (this.selectedFolderId === null) {
      return;
    }

    this.mediaLibraryService.deleteMedia(this.selectedFolderId, fileName).subscribe(() => {
      this.toastrService.success('Media deleted.');
      this.loadInitialMedia(this.selectedFolderId!);
    });
  }

  private loadInitialMedia(folderId: string): void {
    this.mediaFiles = [];
    this.allMediaFiles = [];
    this.hasMoreMedia = false;
    this.isLoadingMoreMedia = false;
    this.mediaLibraryService.getMedia(folderId).subscribe({
      next: (mediaFiles) => {
        this.allMediaFiles = mediaFiles;
        this.hasMoreMedia = mediaFiles.length > 0;
        this.loadMoreMedia(true);
      },
      error: () => {
        this.hasMoreMedia = false;
      },
    });
  }

  private loadMoreMediaIfNeeded(): void {
    if (this.selectedFolderId === null || this.isLoadingMoreMedia || !this.hasMoreMedia) {
      return;
    }

    const distanceFromBottom =
      document.documentElement.scrollHeight - (window.innerHeight + window.scrollY);

    if (distanceFromBottom <= 400) {
      this.loadMoreMedia();
    }
  }

  private loadMoreMedia(reset = false): void {
    if (this.isLoadingMoreMedia || (!reset && !this.hasMoreMedia)) {
      return;
    }

    const offset = reset ? 0 : this.mediaFiles?.length ?? 0;
    const nextItems = this.allMediaFiles.slice(offset, offset + this.mediaPageSize);
    const nextMediaFiles = reset
      ? nextItems
      : [...(this.mediaFiles ?? []), ...nextItems];

    this.mediaFiles = nextMediaFiles;
    this.hasMoreMedia = nextMediaFiles.length < this.allMediaFiles.length;

    if (nextItems.length === 0) {
      this.isLoadingMoreMedia = false;
      return;
    }

    this.isLoadingMoreMedia = true;
    queueMicrotask(() => {
      this.isLoadingMoreMedia = false;
      this.loadMoreMediaIfNeeded();
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
    const dialogRef = this.dialogService.open(GenerateImageComponent, {
      header: 'Generate Image',
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

    dialogRef.onClose.subscribe((image: Blob | undefined) => {
      if (!image) {
        return;
      }

      const file = new File([image], 'generated-image.png', {
        type: image.type || 'image/png',
      });

      this.openUploadMediaDialog({
        initialFile: file,
        initialName: file.name,
      });
    });
  }
}
