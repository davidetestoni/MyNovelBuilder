import {
  Component,
  OnDestroy,
  OnInit,
  inject,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { ConfirmationService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { SelectModule } from 'primeng/select';
import { SelectButtonModule } from 'primeng/selectbutton';
import {
  AddMediaFolderDialogComponent,
  AddMediaFolderDialogResult,
} from '../../components/add-media-folder-dialog/add-media-folder-dialog.component';
import { ImageGenerationStudioComponent } from '../../components/image-generation-studio/image-generation-studio.component';
import { MediaFolderComponent } from '../../components/media-folder/media-folder.component';
import { MediaLibraryService } from '../../services/media-library.service';
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
    SelectButtonModule,
    ConfirmDialogModule,
    ImageGenerationStudioComponent,
    MediaFolderComponent,
  ],
  providers: [DialogService, ConfirmationService],
})
export class MediaLibraryComponent implements OnInit, OnDestroy {
  private static readonly LAST_SELECTED_FOLDER_STORAGE_KEY =
    'media-library:last-selected-folder-id';

  private mediaLibraryService = inject(MediaLibraryService);
  private toastrService = inject(ToastrService);
  private dialogService = inject(DialogService);
  private confirmationService = inject(ConfirmationService);
  private dialogRef: DynamicDialogRef | null = null;

  folderOptions: { label: string; value: string; path: string }[] = [];
  layoutOptions = [
    { label: 'Folder', value: 'folder' },
    { label: 'Studio', value: 'studio' },
  ];
  folders: MediaFolderDto[] | null = null;
  selectedFolderId: string | null = null;
  creatingFolder = false;
  mediaFolderRefreshVersion = 0;
  selectedLayout: 'folder' | 'studio' = 'studio';

  get selectedFolder(): MediaFolderDto | null {
    return (
      this.folders?.find((folder) => folder.id === this.selectedFolderId) ?? null
    );
  }

  get isImageGenerationStudioVisible(): boolean {
    return this.selectedLayout === 'studio';
  }

  ngOnInit(): void {
    this.loadFolders();
  }

  ngOnDestroy(): void {
    this.dialogRef?.close();
  }

  loadFolders(selectFolderId?: string | null): void {
    this.mediaLibraryService.getFolders().subscribe((folders) => {
      this.folders = folders;
      this.folderOptions = folders.map((folder) => ({
        label: folder.name,
        value: folder.id,
        path: folder.path,
      }));

      const storedFolderId = this.getStoredSelectedFolderId();
      const nextFolderId = this.resolveNextFolderId(
        folders,
        selectFolderId,
        storedFolderId,
      );

      this.setSelectedFolderId(nextFolderId);

      if (nextFolderId === null) {
        return;
      }
    });
  }

  selectFolder(folderId: string | null): void {
    this.setSelectedFolderId(folderId);
  }

  onStudioMediaSaved(): void {
    this.mediaFolderRefreshVersion += 1;
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

  private resolveNextFolderId(
    folders: MediaFolderDto[],
    requestedFolderId?: string | null,
    storedFolderId?: string | null,
  ): string | null {
    if (requestedFolderId && folders.some((folder) => folder.id === requestedFolderId)) {
      return requestedFolderId;
    }

    if (storedFolderId && folders.some((folder) => folder.id === storedFolderId)) {
      return storedFolderId;
    }

    return folders[0]?.id ?? null;
  }

  private setSelectedFolderId(folderId: string | null): void {
    this.selectedFolderId = folderId;

    if (folderId === null) {
      localStorage.removeItem(
        MediaLibraryComponent.LAST_SELECTED_FOLDER_STORAGE_KEY,
      );
      return;
    }

    localStorage.setItem(
      MediaLibraryComponent.LAST_SELECTED_FOLDER_STORAGE_KEY,
      folderId,
    );
  }

  private getStoredSelectedFolderId(): string | null {
    return localStorage.getItem(
      MediaLibraryComponent.LAST_SELECTED_FOLDER_STORAGE_KEY,
    );
  }
}
