import { TestBed } from '@angular/core/testing';
import { ToastrService } from 'ngx-toastr';
import { ConfirmationService } from 'primeng/api';
import type { Confirmation } from 'primeng/api';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { of, Subject } from 'rxjs';
import { MediaLibraryService } from '../../services/media-library.service';
import type { MediaFolderDto } from '../../types/dtos/media-library/media-folder.dto';
import { MediaLibraryComponent } from './media-library.component';

describe('MediaLibraryComponent workflows', () => {
  const selectedFolderStorageKey = 'media-library:last-selected-folder-id';

  let component: MediaLibraryComponent;
  let mediaLibraryService: jasmine.SpyObj<MediaLibraryService>;
  let toastrService: jasmine.SpyObj<ToastrService>;
  let confirmationService: jasmine.SpyObj<ConfirmationService>;
  let dialogService: jasmine.SpyObj<DialogService>;

  const folder = (
    id: string,
    name = `Folder ${id}`,
    path = `/media/${id}`,
  ): MediaFolderDto => ({
    id,
    name,
    path,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  });

  beforeEach(() => {
    mediaLibraryService = jasmine.createSpyObj<MediaLibraryService>(
      'MediaLibraryService',
      ['getFolders', 'createFolder', 'deleteFolder'],
    );
    toastrService = jasmine.createSpyObj<ToastrService>('ToastrService', [
      'success',
    ]);
    confirmationService = jasmine.createSpyObj<ConfirmationService>(
      'ConfirmationService',
      ['confirm'],
    );
    dialogService = jasmine.createSpyObj<DialogService>('DialogService', [
      'open',
    ]);

    mediaLibraryService.getFolders.and.returnValue(of([]));

    TestBed.configureTestingModule({
      providers: [
        { provide: MediaLibraryService, useValue: mediaLibraryService },
        { provide: ToastrService, useValue: toastrService },
        { provide: ConfirmationService, useValue: confirmationService },
        { provide: DialogService, useValue: dialogService },
      ],
    });

    component = TestBed.runInInjectionContext(
      () => new MediaLibraryComponent(),
    );
    localStorage.clear();
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('loads folder options and restores the stored selection', () => {
    const folders = [folder('one', 'Images'), folder('two', 'Videos')];
    const foldersResponse = new Subject<MediaFolderDto[]>();
    mediaLibraryService.getFolders.and.returnValue(foldersResponse);
    localStorage.setItem(selectedFolderStorageKey, 'two');

    component.ngOnInit();

    expect(component.folders).toBeNull();

    foldersResponse.next(folders);
    foldersResponse.complete();

    expect(component.folders).toBe(folders);
    expect(component.folderOptions).toEqual([
      { label: 'Images', value: 'one', path: '/media/one' },
      { label: 'Videos', value: 'two', path: '/media/two' },
    ]);
    expect(component.selectedFolder).toBe(folders[1]);
    expect(component.selectedFolderId).toBe('two');
  });

  it('prefers a requested selection and persists it', () => {
    mediaLibraryService.getFolders.and.returnValue(
      of([folder('one'), folder('two')]),
    );
    localStorage.setItem(selectedFolderStorageKey, 'one');

    component.loadFolders('two');

    expect(component.selectedFolderId).toBe('two');
    expect(localStorage.getItem(selectedFolderStorageKey)).toBe('two');
  });

  it('clears an obsolete stored selection when there are no folders', () => {
    localStorage.setItem(selectedFolderStorageKey, 'missing');

    component.loadFolders();

    expect(component.folders).toEqual([]);
    expect(component.selectedFolderId).toBeNull();
    expect(localStorage.getItem(selectedFolderStorageKey)).toBeNull();
  });

  it('tracks folder creation and reloads with the created folder selected', () => {
    const createdFolder = folder('created');
    const createResponse = new Subject<MediaFolderDto>();
    mediaLibraryService.createFolder.and.returnValue(createResponse);
    const loadFolders = spyOn(component, 'loadFolders');

    component.createFolder({
      name: createdFolder.name,
      path: createdFolder.path,
    });

    expect(component.creatingFolder).toBeTrue();
    expect(mediaLibraryService.createFolder).toHaveBeenCalledOnceWith(
      createdFolder.name,
      createdFolder.path,
    );

    createResponse.next(createdFolder);
    createResponse.complete();

    expect(component.creatingFolder).toBeFalse();
    expect(toastrService.success).toHaveBeenCalledOnceWith(
      'Media folder linked.',
    );
    expect(loadFolders).toHaveBeenCalledOnceWith('created');
  });

  it('clears the creation state when folder creation fails', () => {
    const createResponse = new Subject<MediaFolderDto>();
    mediaLibraryService.createFolder.and.returnValue(createResponse);

    component.createFolder({ name: 'Images', path: '/media/images' });
    createResponse.error(new Error('request failed'));

    expect(component.creatingFolder).toBeFalse();
    expect(toastrService.success).not.toHaveBeenCalled();
  });

  it('creates a folder from the dialog result and closes the dialog on destroy', () => {
    const onClose = new Subject<{ name: string; path: string } | undefined>();
    const dialogRef = {
      onClose,
      close: jasmine.createSpy('close'),
    } as unknown as DynamicDialogRef;
    dialogService.open.and.returnValue(dialogRef);
    const createFolder = spyOn(component, 'createFolder');

    component.openAddFolderDialog();
    onClose.next({ name: 'Images', path: '/media/images' });

    expect(createFolder).toHaveBeenCalledOnceWith({
      name: 'Images',
      path: '/media/images',
    });

    component.ngOnDestroy();

    expect(dialogRef.close).toHaveBeenCalledTimes(1);
  });

  it('deletes the selected folder only after confirmation', () => {
    const selectedFolder = folder('selected', 'Selected');
    component.folders = [selectedFolder, folder('remaining')];
    component.selectFolder(selectedFolder.id);
    mediaLibraryService.deleteFolder.and.returnValue(of(undefined));
    const loadFolders = spyOn(component, 'loadFolders');

    component.confirmDeleteSelectedFolder();

    expect(mediaLibraryService.deleteFolder).not.toHaveBeenCalled();

    const confirmation = confirmationService.confirm.calls.mostRecent()
      .args[0] as Confirmation;
    confirmation.accept?.();

    expect(mediaLibraryService.deleteFolder).toHaveBeenCalledOnceWith(
      selectedFolder.id,
    );
    expect(toastrService.success).toHaveBeenCalledOnceWith(
      'Media folder unlinked.',
    );
    expect(loadFolders).toHaveBeenCalledOnceWith(null);
  });
});
