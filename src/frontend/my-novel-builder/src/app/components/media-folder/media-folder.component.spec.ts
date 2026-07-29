import { ElementRef, SimpleChange } from '@angular/core';
import {
  fakeAsync,
  flushMicrotasks,
  TestBed,
} from '@angular/core/testing';
import { ToastrService } from 'ngx-toastr';
import { ConfirmationService } from 'primeng/api';
import type { Confirmation } from 'primeng/api';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { of, Subject, throwError } from 'rxjs';
import { LocalStorageService } from '../../services/local-storage.service';
import { MediaLibraryService } from '../../services/media-library.service';
import { MediaFileDto } from '../../types/dtos/media-library/media-file.dto';
import { MediaFolderDto } from '../../types/dtos/media-library/media-folder.dto';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { DescribeImageComponent } from '../describe-image/describe-image.component';
import { EditImageComponent } from '../edit-image/edit-image.component';
import { GenerateMediaComponent } from '../generate-media/generate-media.component';
import { ImageSourceSelectorComponent } from '../image-source-selector/image-source-selector.component';
import { UploadMediaDialogComponent } from '../upload-media-dialog/upload-media-dialog.component';
import { MediaFolderComponent } from './media-folder.component';

describe('MediaFolderComponent workflows', () => {
  const originalClipboardDescriptor = Object.getOwnPropertyDescriptor(
    navigator,
    'clipboard',
  );
  const originalResizeObserverDescriptor = Object.getOwnPropertyDescriptor(
    window,
    'ResizeObserver',
  );

  let component: MediaFolderComponent;
  let mediaLibraryService: jasmine.SpyObj<MediaLibraryService>;
  let localStorageService: jasmine.SpyObj<LocalStorageService>;
  let toastrService: jasmine.SpyObj<ToastrService>;
  let confirmationService: jasmine.SpyObj<ConfirmationService>;
  let dialogService: jasmine.SpyObj<DialogService>;
  let createObjectUrlSpy: jasmine.Spy;
  let revokeObjectUrlSpy: jasmine.Spy;
  let fetchSpy: jasmine.Spy;
  let resizeCallback: ResizeObserverCallback | null;
  let observeSpy: jasmine.Spy;
  let disconnectSpy: jasmine.Spy;
  let objectUrlIndex: number;

  const folder = (id: string): MediaFolderDto => ({
    id,
    name: `Folder ${id}`,
    path: `/media/${id}`,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  });

  const mediaFile = (
    fileName: string,
    sizeBytes = 100,
    lastModifiedAt = '2026-01-01T00:00:00Z',
  ): MediaFileDto => ({
    fileName,
    sizeBytes,
    lastModifiedAt,
  });

  const preview = (
    fileName: string,
    url: string | null = `blob:${fileName}`,
    isVideo = false,
  ) => ({
    ...mediaFile(fileName),
    url,
    isVideo,
  });

  const dialogRef = <T>(): DynamicDialogRef => {
    const onClose = new Subject<T | undefined>();
    return {
      onClose,
      close: jasmine.createSpy('close'),
    } as unknown as DynamicDialogRef;
  };

  const createComponent = (): MediaFolderComponent =>
    TestBed.runInInjectionContext(() => new MediaFolderComponent());

  beforeEach(() => {
    mediaLibraryService = jasmine.createSpyObj<MediaLibraryService>(
      'MediaLibraryService',
      ['getMedia', 'getMediaBlob', 'uploadMedia', 'deleteMedia'],
    );
    localStorageService = jasmine.createSpyObj<LocalStorageService>(
      'LocalStorageService',
      ['getStringForKey', 'setStringForKey'],
    );
    toastrService = jasmine.createSpyObj<ToastrService>('ToastrService', [
      'error',
      'success',
    ]);
    confirmationService = jasmine.createSpyObj<ConfirmationService>(
      'ConfirmationService',
      ['confirm'],
    );
    dialogService = jasmine.createSpyObj<DialogService>('DialogService', [
      'open',
    ]);

    localStorageService.getStringForKey.and.returnValue(null);
    mediaLibraryService.getMedia.and.returnValue(of([]));
    mediaLibraryService.getMediaBlob.and.returnValue(
      of(new Blob(['image'], { type: 'image/png' })),
    );
    mediaLibraryService.uploadMedia.and.returnValue(
      of(mediaFile('uploaded.png')),
    );
    mediaLibraryService.deleteMedia.and.returnValue(of(undefined));

    objectUrlIndex = 0;
    createObjectUrlSpy = spyOn(URL, 'createObjectURL').and.callFake(
      () => `blob:folder-${++objectUrlIndex}`,
    );
    revokeObjectUrlSpy = spyOn(URL, 'revokeObjectURL');
    fetchSpy = spyOn(window, 'fetch').and.resolveTo(
      new Response(null, { status: 404 }),
    );

    resizeCallback = null;
    observeSpy = jasmine.createSpy('observe');
    disconnectSpy = jasmine.createSpy('disconnect');
    class ResizeObserverStub {
      constructor(callback: ResizeObserverCallback) {
        resizeCallback = callback;
      }

      observe = observeSpy;
      disconnect = disconnectSpy;
      unobserve(): void {}
    }
    Object.defineProperty(window, 'ResizeObserver', {
      configurable: true,
      value: ResizeObserverStub,
    });

    TestBed.configureTestingModule({
      providers: [
        { provide: MediaLibraryService, useValue: mediaLibraryService },
        { provide: LocalStorageService, useValue: localStorageService },
        { provide: ToastrService, useValue: toastrService },
        { provide: ConfirmationService, useValue: confirmationService },
        { provide: DialogService, useValue: dialogService },
      ],
    });

    component = createComponent();
  });

  afterEach(() => {
    component.ngOnDestroy();

    if (originalClipboardDescriptor === undefined) {
      delete (navigator as { clipboard?: Clipboard }).clipboard;
    } else {
      Object.defineProperty(
        navigator,
        'clipboard',
        originalClipboardDescriptor,
      );
    }

    if (originalResizeObserverDescriptor === undefined) {
      delete (window as { ResizeObserver?: typeof ResizeObserver })
        .ResizeObserver;
    } else {
      Object.defineProperty(
        window,
        'ResizeObserver',
        originalResizeObserverDescriptor,
      );
    }
  });

  it('starts with pagination defaults when no preference is stored', () => {
    expect(component.currentPageFirst).toBe(0);
    expect(component.currentPageSize).toBe(8);
    expect(component.mediaFiles).toBeNull();
    expect(component.mediaPreviews).toBeNull();
    expect(localStorageService.getStringForKey).toHaveBeenCalledOnceWith(
      LocalStorageKey.MediaFolderRowsPerPage,
    );
  });

  it('restores and normalizes the stored row count', () => {
    localStorageService.getStringForKey.and.returnValue('3.8');

    component = createComponent();

    const content = document.createElement('div');
    Object.defineProperty(content, 'clientWidth', { value: 850 });
    component.mediaFolderContentRef = new ElementRef(content);

    expect(component.currentPageSize).toBe(12);
  });

  it('caps stored rows and ignores invalid row preferences', () => {
    localStorageService.getStringForKey.and.returnValue('99');
    component = createComponent();
    const wideContent = document.createElement('div');
    Object.defineProperty(wideContent, 'clientWidth', { value: 424 });
    component.mediaFolderContentRef = new ElementRef(wideContent);
    expect(component.currentPageSize).toBe(10);

    component.ngOnDestroy();
    localStorageService.getStringForKey.and.returnValue('0');
    component = createComponent();
    const narrowContent = document.createElement('div');
    Object.defineProperty(narrowContent, 'clientWidth', { value: 200 });
    component.mediaFolderContentRef = new ElementRef(narrowContent);
    expect(component.currentPageSize).toBe(2);
  });

  it('loads media when a folder is selected', () => {
    const files = [mediaFile('one.png'), mediaFile('two.png')];
    mediaLibraryService.getMedia.and.returnValue(of(files));

    component.folder = folder('images');

    expect(mediaLibraryService.getMedia).toHaveBeenCalledOnceWith('images');
    expect(component.allMediaFiles).toBe(files);
    expect(component.mediaFiles).toEqual(files);
    expect(component.currentPageFirst).toBe(0);
  });

  it('does not reload when the same folder object or id is assigned', () => {
    component.folder = folder('images');
    mediaLibraryService.getMedia.calls.reset();

    component.folder = folder('images');

    expect(mediaLibraryService.getMedia).not.toHaveBeenCalled();
  });

  it('clears folder state and cached previews when selection is removed', () => {
    mediaLibraryService.getMedia.and.returnValue(of([mediaFile('one.png')]));
    component.folder = folder('images');
    expect(component.mediaPreviews?.[0].url).toBe('blob:folder-1');

    component.folder = null;

    expect(component.allMediaFiles).toEqual([]);
    expect(component.currentPageFirst).toBe(0);
    expect(component.mediaFiles).toBeNull();
    expect(component.mediaPreviews).toBeNull();
    expect(revokeObjectUrlSpy).toHaveBeenCalledOnceWith('blob:folder-1');
  });

  it('ignores a response from a previously selected folder', () => {
    const firstResponse = new Subject<MediaFileDto[]>();
    mediaLibraryService.getMedia.and.returnValues(
      firstResponse,
      of([mediaFile('current.png')]),
    );

    component.folder = folder('first');
    component.folder = folder('second');
    firstResponse.next([mediaFile('stale.png')]);

    expect(component.allMediaFiles).toEqual([mediaFile('current.png')]);
  });

  it('keeps the newest response when the same folder is refreshed twice', () => {
    const olderResponse = new Subject<MediaFileDto[]>();
    const newerResponse = new Subject<MediaFileDto[]>();
    mediaLibraryService.getMedia.and.returnValues(
      of([]),
      olderResponse,
      newerResponse,
    );
    component.folder = folder('images');

    component.ngOnChanges({
      refreshVersion: new SimpleChange(0, 1, false),
    });
    component.ngOnChanges({
      refreshVersion: new SimpleChange(1, 2, false),
    });
    newerResponse.next([mediaFile('new.png')]);
    olderResponse.next([mediaFile('old.png')]);

    expect(component.allMediaFiles).toEqual([mediaFile('new.png')]);
  });

  it('reports media-list failures and settles the loading state', () => {
    mediaLibraryService.getMedia.and.returnValue(
      throwError(() => new Error('failed')),
    );

    component.folder = folder('images');

    expect(component.mediaFiles).toEqual([]);
    expect(component.mediaPreviews).toEqual([]);
    expect(toastrService.error).toHaveBeenCalledOnceWith(
      'Failed to load media.',
    );
  });

  it('reloads on later refresh changes but ignores the initial change', () => {
    component.folder = folder('images');
    mediaLibraryService.getMedia.calls.reset();

    component.ngOnChanges({
      refreshVersion: new SimpleChange(undefined, 0, true),
    });
    expect(mediaLibraryService.getMedia).not.toHaveBeenCalled();

    component.ngOnChanges({
      refreshVersion: new SimpleChange(0, 1, false),
    });
    expect(mediaLibraryService.getMedia).toHaveBeenCalledOnceWith('images');
  });

  it('calculates visible-range and paginator state', () => {
    expect(component.showingMediaStart).toBe(0);
    expect(component.showingMediaEnd).toBe(0);
    expect(component.shouldShowPaginator).toBeFalse();

    component.allMediaFiles = Array.from({ length: 10 }, (_, index) =>
      mediaFile(`${index}.png`),
    );
    component.currentPageFirst = 8;

    expect(component.showingMediaStart).toBe(9);
    expect(component.showingMediaEnd).toBe(10);
    expect(component.shouldShowPaginator).toBeTrue();
  });

  it('derives sorted page-size options from the grid columns', () => {
    const content = document.createElement('div');
    Object.defineProperty(content, 'clientWidth', { value: 636 });
    component.mediaFolderContentRef = new ElementRef(content);

    expect(component.rowsPerPageOptions).toEqual([3, 6, 9, 12, 15]);
  });

  it('changes pages, stores row preferences, and updates the visible slice', () => {
    component.folder = folder('images');
    component.allMediaFiles = Array.from({ length: 12 }, (_, index) =>
      mediaFile(`${index}.png`),
    );

    component.onPageChange({ first: 6, rows: 6 });

    expect(component.currentPageFirst).toBe(6);
    expect(component.currentPageSize).toBe(5);
    expect(component.mediaFiles?.map(({ fileName }) => fileName)).toEqual([
      '6.png',
      '7.png',
      '8.png',
      '9.png',
      '10.png',
    ]);
    expect(localStorageService.setStringForKey).toHaveBeenCalledOnceWith(
      LocalStorageKey.MediaFolderRowsPerPage,
      '5',
    );
  });

  it('uses existing pagination values when paginator fields are absent', () => {
    component.folder = folder('images');
    component.currentPageFirst = 2;
    component.allMediaFiles = Array.from({ length: 10 }, (_, index) =>
      mediaFile(`${index}.png`),
    );

    component.onPageChange({});

    expect(component.currentPageFirst).toBe(2);
    expect(component.currentPageSize).toBe(8);
    expect(localStorageService.setStringForKey).not.toHaveBeenCalled();
  });

  it('updates page size from layout while retaining the page index', () => {
    component.folder = folder('images');
    component.allMediaFiles = Array.from({ length: 30 }, (_, index) =>
      mediaFile(`${index}.png`),
    );
    component.currentPageFirst = 16;

    const content = document.createElement('div');
    Object.defineProperty(content, 'clientWidth', { value: 636 });
    component.mediaFolderContentRef = new ElementRef(content);

    expect(observeSpy).toHaveBeenCalledOnceWith(content);
    expect(resizeCallback).not.toBeNull();
    expect(component.currentPageSize).toBe(6);
    expect(component.currentPageFirst).toBe(12);
  });

  it('disconnects a previous layout observer and ignores zero-width panels', () => {
    const first = document.createElement('div');
    Object.defineProperty(first, 'clientWidth', { value: 0 });
    component.mediaFolderContentRef = new ElementRef(first);
    expect(component.currentPageSize).toBe(8);

    component.mediaFolderContentRef = new ElementRef(
      document.createElement('div'),
    );
    expect(disconnectSpy).toHaveBeenCalledTimes(1);
  });

  it('clamps pagination to the final available page', () => {
    component.folder = folder('images');
    component.currentPageFirst = 24;
    component.currentPageSize = 8;
    component.allMediaFiles = Array.from({ length: 10 }, (_, index) =>
      mediaFile(`${index}.png`),
    );

    component['updateVisibleMediaFiles']();

    expect(component.currentPageFirst).toBe(8);
    expect(component.mediaFiles?.length).toBe(2);
  });

  it('loads ordered image previews and creates object URLs', () => {
    mediaLibraryService.getMedia.and.returnValue(
      of([mediaFile('first.png'), mediaFile('second.jpg')]),
    );

    component.folder = folder('images');

    expect(mediaLibraryService.getMediaBlob.calls.allArgs()).toEqual([
      ['images', 'first.png'],
      ['images', 'second.jpg'],
    ]);
    expect(component.mediaPreviews?.map(({ fileName, url }) => ({
      fileName,
      url,
    }))).toEqual([
      { fileName: 'first.png', url: 'blob:folder-1' },
      { fileName: 'second.jpg', url: 'blob:folder-2' },
    ]);
  });

  it('recognizes videos from MIME type or filename', () => {
    mediaLibraryService.getMedia.and.returnValue(
      of([mediaFile('stream.bin'), mediaFile('clip.MOV')]),
    );
    mediaLibraryService.getMediaBlob.and.returnValues(
      of(new Blob(['video'], { type: 'video/webm' })),
      of(new Blob(['video'])),
    );

    component.folder = folder('videos');

    expect(component.mediaPreviews?.map(({ isVideo }) => isVideo)).toEqual([
      true,
      true,
    ]);
  });

  it('keeps unavailable previews when individual blob loading fails', () => {
    mediaLibraryService.getMedia.and.returnValue(
      of([mediaFile('missing.png')]),
    );
    mediaLibraryService.getMediaBlob.and.returnValue(
      throwError(() => new Error('failed')),
    );

    component.folder = folder('images');

    expect(component.mediaPreviews).toEqual([
      jasmine.objectContaining({
        fileName: 'missing.png',
        url: null,
        isVideo: false,
      }),
    ]);
  });

  it('reuses unchanged cached previews', () => {
    const file = mediaFile('cached.png');
    mediaLibraryService.getMedia.and.returnValue(of([file]));
    component.folder = folder('images');
    mediaLibraryService.getMediaBlob.calls.reset();

    component.ngOnChanges({
      refreshVersion: new SimpleChange(0, 1, false),
    });

    expect(mediaLibraryService.getMediaBlob).not.toHaveBeenCalled();
    expect(component.mediaPreviews?.[0].url).toBe('blob:folder-1');
  });

  it('revokes and reloads a changed cached preview', () => {
    mediaLibraryService.getMedia.and.returnValues(
      of([mediaFile('changed.png', 100)]),
      of([mediaFile('changed.png', 200)]),
    );
    component.folder = folder('images');

    component.ngOnChanges({
      refreshVersion: new SimpleChange(0, 1, false),
    });

    expect(revokeObjectUrlSpy).toHaveBeenCalledWith('blob:folder-1');
    expect(mediaLibraryService.getMediaBlob).toHaveBeenCalledTimes(2);
    expect(component.mediaPreviews?.[0].url).toBe('blob:folder-2');
  });

  it('revokes previews removed by a refresh', () => {
    mediaLibraryService.getMedia.and.returnValues(
      of([mediaFile('removed.png'), mediaFile('kept.png')]),
      of([mediaFile('kept.png')]),
    );
    component.folder = folder('images');

    component.ngOnChanges({
      refreshVersion: new SimpleChange(0, 1, false),
    });

    expect(revokeObjectUrlSpy).toHaveBeenCalledOnceWith('blob:folder-1');
    expect(component.mediaPreviews?.map(({ fileName }) => fileName)).toEqual([
      'kept.png',
    ]);
  });

  it('revokes all cached previews when changing folders', () => {
    mediaLibraryService.getMedia.and.returnValues(
      of([mediaFile('first.png')]),
      of([mediaFile('second.png')]),
    );
    component.folder = folder('first');

    component.folder = folder('second');

    expect(revokeObjectUrlSpy).toHaveBeenCalledWith('blob:folder-1');
    expect(component.mediaPreviews?.[0].fileName).toBe('second.png');
  });

  it('prompts for a folder before opening the add-media selector', () => {
    component.openAddMediaDialog();

    expect(toastrService.error).toHaveBeenCalledOnceWith(
      'Select a media folder first.',
    );
    expect(dialogService.open).not.toHaveBeenCalled();
  });

  it('routes selector choices to upload and generation dialogs', () => {
    component.folder = folder('images');
    const selectorClose = new Subject<
      'upload' | 'generate' | 'clipboard' | undefined
    >();
    dialogService.open.and.returnValue({
      onClose: selectorClose,
      close: jasmine.createSpy('close'),
    } as unknown as DynamicDialogRef);
    const uploadDialog = spyOn(
      component as unknown as {
        openUploadMediaDialog(): void;
      },
      'openUploadMediaDialog',
    );
    const generateDialog = spyOn(
      component as unknown as {
        openGenerateImageDialog(): void;
      },
      'openGenerateImageDialog',
    );

    component.openAddMediaDialog();
    selectorClose.next('upload');
    selectorClose.next('generate');

    expect(dialogService.open).toHaveBeenCalledWith(
      ImageSourceSelectorComponent,
      jasmine.objectContaining({ header: 'Add Media', modal: true }),
    );
    expect(uploadDialog).toHaveBeenCalledTimes(1);
    expect(generateDialog).toHaveBeenCalledTimes(1);
  });

  it('reads a clipboard image selected by the add-media dialog', fakeAsync(() => {
    component.folder = folder('images');
    const selectorClose = new Subject<
      'upload' | 'generate' | 'clipboard' | undefined
    >();
    dialogService.open.and.returnValue({
      onClose: selectorClose,
      close: jasmine.createSpy('close'),
    } as unknown as DynamicDialogRef);
    const uploadDialog = spyOn(
      component as unknown as {
        openUploadMediaDialog(data: {
          initialFile: File;
          initialName: string;
        }): void;
      },
      'openUploadMediaDialog',
    );
    const clipboardBlob = new Blob(['image'], { type: 'image/webp' });
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: {
        read: () =>
          Promise.resolve([
            {
              types: ['image/webp'],
              getType: () => Promise.resolve(clipboardBlob),
            },
          ]),
      },
    });

    component.openAddMediaDialog();
    selectorClose.next('clipboard');
    flushMicrotasks();

    expect(uploadDialog).toHaveBeenCalledTimes(1);
    const data = uploadDialog.calls.mostRecent().args[0];
    expect(data.initialFile.name).toBe('clipboard-image.webp');
    expect(data.initialName).toBe('clipboard-image');
  }));

  it('reports clipboard failures', fakeAsync(() => {
    component.folder = folder('images');
    const selectorClose = new Subject<
      'upload' | 'generate' | 'clipboard' | undefined
    >();
    dialogService.open.and.returnValue({
      onClose: selectorClose,
      close: jasmine.createSpy('close'),
    } as unknown as DynamicDialogRef);
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: {
        read: () => Promise.reject(new Error('Clipboard permission denied')),
      },
    });

    component.openAddMediaDialog();
    selectorClose.next('clipboard');
    flushMicrotasks();

    expect(toastrService.error).toHaveBeenCalledWith(
      'Clipboard permission denied',
    );
  }));

  it('does nothing when a dialog service declines to open a dialog', () => {
    component.folder = folder('images');
    dialogService.open.and.returnValue(null);

    component.openAddMediaDialog();
    component['openUploadMediaDialog']();
    component['openGenerateImageDialog']();

    expect(dialogService.open).toHaveBeenCalledTimes(3);
  });

  it('uploads the trimmed result returned by the upload dialog', () => {
    component.folder = folder('images');
    const onClose = new Subject<
      { name: string; file: File } | undefined
    >();
    dialogService.open.and.returnValue({
      onClose,
      close: jasmine.createSpy('close'),
    } as unknown as DynamicDialogRef);
    const file = new File(['image'], 'original.png', { type: 'image/png' });

    component['openUploadMediaDialog']();
    onClose.next({ name: '  portrait  ', file });

    expect(dialogService.open).toHaveBeenCalledWith(
      UploadMediaDialogComponent,
      jasmine.objectContaining({ header: 'Upload Media' }),
    );
    expect(mediaLibraryService.uploadMedia).toHaveBeenCalledOnceWith(
      'images',
      'portrait',
      file,
    );
  });

  it('validates upload folder, file, and name before calling the service', () => {
    const file = new File(['image'], 'image.png');

    component['uploadMedia']({ name: 'image', file });
    component.folder = folder('images');
    component['uploadMedia']({
      name: 'image',
      file: null as unknown as File,
    });
    component['uploadMedia']({ name: '   ', file });

    expect(toastrService.error.calls.allArgs()).toEqual([
      ['Select a media folder first.'],
      ['Choose a file to upload.'],
      ['Media name is required.'],
    ]);
    expect(mediaLibraryService.uploadMedia).not.toHaveBeenCalled();
  });

  it('prevents duplicate uploads while one is active', () => {
    const response = new Subject<MediaFileDto>();
    mediaLibraryService.uploadMedia.and.returnValue(response);
    component.folder = folder('images');
    const file = new File(['image'], 'image.png');

    component['uploadMedia']({ name: 'first', file });
    component['uploadMedia']({ name: 'second', file });

    expect(component.uploadingMedia).toBeTrue();
    expect(mediaLibraryService.uploadMedia).toHaveBeenCalledOnceWith(
      'images',
      'first',
      file,
    );
  });

  it('settles a successful upload and reloads its current folder', () => {
    const response = new Subject<MediaFileDto>();
    mediaLibraryService.uploadMedia.and.returnValue(response);
    component.folder = folder('images');
    mediaLibraryService.getMedia.calls.reset();

    component['uploadMedia']({
      name: 'image',
      file: new File(['image'], 'image.png'),
    });
    response.next(mediaFile('image.png'));
    response.complete();

    expect(component.uploadingMedia).toBeFalse();
    expect(toastrService.success).toHaveBeenCalledOnceWith('Media uploaded.');
    expect(mediaLibraryService.getMedia).toHaveBeenCalledOnceWith('images');
  });

  it('does not reload a different folder after an upload completes', () => {
    const response = new Subject<MediaFileDto>();
    mediaLibraryService.uploadMedia.and.returnValue(response);
    component.folder = folder('first');
    component['uploadMedia']({
      name: 'image',
      file: new File(['image'], 'image.png'),
    });
    component.folder = folder('second');
    mediaLibraryService.getMedia.calls.reset();

    response.next(mediaFile('image.png'));

    expect(mediaLibraryService.getMedia).not.toHaveBeenCalled();
  });

  it('settles and reports failed uploads', () => {
    mediaLibraryService.uploadMedia.and.returnValue(
      throwError(() => new Error('failed')),
    );
    component.folder = folder('images');

    component['uploadMedia']({
      name: 'image',
      file: new File(['image'], 'image.png'),
    });

    expect(component.uploadingMedia).toBeFalse();
    expect(toastrService.error).toHaveBeenCalledOnceWith(
      'Failed to upload media.',
    );
  });

  it('passes generated media into a prefilled upload dialog', () => {
    const generatedClose = new Subject<Blob | undefined>();
    const generatedRef = {
      onClose: generatedClose,
      close: jasmine.createSpy('close'),
    } as unknown as DynamicDialogRef;
    dialogService.open.and.returnValue(generatedRef);
    const uploadDialog = spyOn(
      component as unknown as {
        openUploadMediaDialog(data: {
          initialFile: File;
          initialName: string;
        }): void;
      },
      'openUploadMediaDialog',
    );

    component['openGenerateImageDialog']();
    generatedClose.next(new Blob(['video'], { type: 'video/mp4' }));

    expect(dialogService.open).toHaveBeenCalledWith(
      GenerateMediaComponent,
      jasmine.objectContaining({ header: 'Generate Media' }),
    );
    const data = uploadDialog.calls.mostRecent().args[0];
    expect(data.initialFile.name).toBe('generated-media.mp4');
    expect(data.initialName).toBe('generated-media.mp4');
  });

  it('edits an image and selects the next available edited filename', () => {
    component.folder = folder('images');
    component.allMediaFiles = [
      mediaFile('portrait.png'),
      mediaFile('portrait-edited.png'),
      mediaFile('portrait-edited-2.png'),
    ];
    const editClose = new Subject<Blob | undefined>();
    dialogService.open.and.returnValue({
      onClose: editClose,
      close: jasmine.createSpy('close'),
    } as unknown as DynamicDialogRef);
    const upload = spyOn(
      component as unknown as {
        uploadMedia(request: { name: string; file: File }): void;
      },
      'uploadMedia',
    );

    component.editImage(preview('portrait.png'));
    editClose.next(new Blob(['edited'], { type: 'image/webp' }));

    expect(mediaLibraryService.getMediaBlob).toHaveBeenCalledWith(
      'images',
      'portrait.png',
    );
    expect(dialogService.open).toHaveBeenCalledWith(
      EditImageComponent,
      jasmine.objectContaining({
        header: 'Edit Image',
        data: jasmine.objectContaining({
          image: jasmine.any(File),
        }),
      }),
    );
    const request = upload.calls.mostRecent().args[0];
    expect(request.name).toBe('portrait-edited-3.png');
    expect(request.file.name).toBe('portrait-edited-3.png');
    expect(request.file.type).toBe('image/webp');
  });

  it('ignores edit requests without an image and reports blob failures', () => {
    component.editImage(preview('image.png'));
    component.folder = folder('images');
    component.editImage(preview('video.mp4', 'blob:video', true));
    mediaLibraryService.getMediaBlob.and.returnValue(
      throwError(() => new Error('failed')),
    );
    component.editImage(preview('image.png'));

    expect(mediaLibraryService.getMediaBlob).toHaveBeenCalledOnceWith(
      'images',
      'image.png',
    );
    expect(toastrService.error).toHaveBeenCalledOnceWith(
      'Failed to load image',
    );
  });

  it('asks for confirmation before deleting media', () => {
    component.folder = folder('images');

    component.confirmDeleteMedia('portrait.png');

    expect(mediaLibraryService.deleteMedia).not.toHaveBeenCalled();
    const confirmation = confirmationService.confirm.calls.mostRecent()
      .args[0] as Confirmation;
    expect(confirmation.message).toContain('portrait.png');
    confirmation.accept?.();
    expect(mediaLibraryService.deleteMedia).toHaveBeenCalledOnceWith(
      'images',
      'portrait.png',
    );
  });

  it('reports deletion success and reloads only the original folder', () => {
    const response = new Subject<void>();
    mediaLibraryService.deleteMedia.and.returnValue(response);
    component.folder = folder('images');
    component['deleteMedia']('portrait.png');
    mediaLibraryService.getMedia.calls.reset();

    response.next();
    expect(toastrService.success).toHaveBeenCalledOnceWith('Media deleted.');
    expect(mediaLibraryService.getMedia).toHaveBeenCalledOnceWith('images');

    component.folder = folder('other');
    const laterResponse = new Subject<void>();
    mediaLibraryService.deleteMedia.and.returnValue(laterResponse);
    component['deleteMedia']('other.png');
    component.folder = folder('third');
    mediaLibraryService.getMedia.calls.reset();
    laterResponse.next();
    expect(mediaLibraryService.getMedia).not.toHaveBeenCalled();
  });

  it('reports deletion failures', () => {
    mediaLibraryService.deleteMedia.and.returnValue(
      throwError(() => new Error('failed')),
    );
    component.folder = folder('images');

    component['deleteMedia']('portrait.png');

    expect(toastrService.error).toHaveBeenCalledOnceWith(
      'Failed to delete media.',
    );
  });

  it('zooms only available media and resets detail state on close', () => {
    component.zoomMedia(preview('missing.png', null));
    expect(component.zoomedMedia).toBeNull();

    component.zoomMedia(preview('image.png'));
    expect(component.zoomedMedia?.fileName).toBe('image.png');
    component.zoomedMediaPrompt = 'prompt';
    component.zoomedMediaDescription = 'description';

    component.unzoomMedia();

    expect(component.zoomedMedia).toBeNull();
    expect(component.zoomedMediaPrompt).toBeNull();
    expect(component.zoomedMediaDescription).toBeNull();
    expect(component.isZoomedMediaPromptLoading).toBeFalse();
  });

  it('navigates zoomed media with wrapping keyboard controls', () => {
    const first = preview('first.png');
    const second = preview('second.png');
    component.mediaPreviews = [first, second];
    component.zoomMedia(first);
    const right = new KeyboardEvent('keydown', { key: 'ArrowRight' });
    spyOn(right, 'preventDefault');

    component.onDocumentKeyDown(right);
    expect(component.zoomedMedia?.fileName).toBe('second.png');

    component.onDocumentKeyDown(
      new KeyboardEvent('keydown', { key: 'ArrowRight' }),
    );
    expect(component.zoomedMedia?.fileName).toBe('first.png');

    component.onDocumentKeyDown(
      new KeyboardEvent('keydown', { key: 'ArrowLeft' }),
    );
    expect(component.zoomedMedia?.fileName).toBe('second.png');
    expect(right.preventDefault).toHaveBeenCalled();
  });

  it('closes zoom with Escape and ignores keys while not zoomed', () => {
    const escape = new KeyboardEvent('keydown', { key: 'Escape' });
    spyOn(escape, 'preventDefault');
    component.onDocumentKeyDown(escape);
    expect(escape.preventDefault).not.toHaveBeenCalled();

    component.zoomMedia(preview('image.png'));
    component.onDocumentKeyDown(escape);

    expect(escape.preventDefault).toHaveBeenCalledTimes(1);
    expect(component.zoomedMedia).toBeNull();
  });

  it('routes zoomed edit and delete actions after closing the overlay', () => {
    component.folder = folder('images');
    const image = preview('image.png');
    const edit = spyOn(component, 'editImage');
    const confirmDelete = spyOn(component, 'confirmDeleteMedia');

    component.zoomMedia(image);
    component.editZoomedMedia();
    expect(component.zoomedMedia).toBeNull();
    expect(edit).toHaveBeenCalledOnceWith(image);

    component.zoomMedia(image);
    component.deleteZoomedMedia();
    expect(component.zoomedMedia).toBeNull();
    expect(confirmDelete).toHaveBeenCalledOnceWith('image.png');
  });

  it('does not let an older metadata request overwrite a video selection', fakeAsync(() => {
    let resolveResponse: ((response: Response) => void) | undefined;
    fetchSpy.and.returnValue(
      new Promise<Response>((resolve) => {
        resolveResponse = resolve;
      }),
    );

    component.zoomMedia(preview('image.png'));
    expect(component.isZoomedMediaPromptLoading).toBeTrue();
    component.zoomMedia(preview('video.mp4', 'blob:video', true));
    expect(component.isZoomedMediaPromptLoading).toBeFalse();

    resolveResponse?.(new Response(new ArrayBuffer(0), { status: 200 }));
    flushMicrotasks();

    expect(component.zoomedMedia?.fileName).toBe('video.mp4');
    expect(component.zoomedMediaPrompt).toBeNull();
    expect(component.isZoomedMediaPromptLoading).toBeFalse();
  }));

  it('settles metadata loading when fetching the image fails', fakeAsync(() => {
    fetchSpy.and.rejectWith(new Error('failed'));

    component.zoomMedia(preview('image.png'));
    flushMicrotasks();

    expect(component.zoomedMediaPrompt).toBeNull();
    expect(component.isZoomedMediaPromptLoading).toBeFalse();
  }));

  it('copies prompt text and falls back to a generated description', fakeAsync(() => {
    const writeText = jasmine.createSpy('writeText').and.resolveTo();
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText },
    });
    component.zoomedMediaPrompt = 'embedded prompt';

    component.copyZoomedMediaPrompt();
    flushMicrotasks();
    expect(writeText).toHaveBeenCalledOnceWith('embedded prompt');
    expect(toastrService.success).toHaveBeenCalledOnceWith(
      'Text copied to clipboard',
    );

    component.zoomedMediaPrompt = null;
    component.zoomedMediaDescription = 'generated description';
    component.copyZoomedMediaPrompt();
    flushMicrotasks();
    expect(writeText).toHaveBeenCalledWith('generated description');
  }));

  it('reports clipboard write failures and ignores empty details', fakeAsync(() => {
    const writeText = jasmine
      .createSpy('writeText')
      .and.rejectWith(new Error('failed'));
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText },
    });

    component.copyZoomedMediaPrompt();
    expect(writeText).not.toHaveBeenCalled();

    component.zoomedMediaPrompt = 'prompt';
    component.copyZoomedMediaPrompt();
    flushMicrotasks();
    expect(toastrService.error).toHaveBeenCalledOnceWith(
      'Failed to copy text',
    );
  }));

  it('describes the zoomed image and trims the returned text', () => {
    component.folder = folder('images');
    component.zoomedMedia = preview('image.png');
    const onClose = new Subject<string | undefined>();
    dialogService.open.and.returnValue({
      onClose,
      close: jasmine.createSpy('close'),
    } as unknown as DynamicDialogRef);

    component.describeZoomedMedia();
    onClose.next('  A moonlit tower  ');

    expect(dialogService.open).toHaveBeenCalledWith(
      DescribeImageComponent,
      jasmine.objectContaining({
        header: 'Describe Image',
        data: jasmine.objectContaining({
          image: jasmine.any(File),
        }),
      }),
    );
    expect(component.zoomedMediaDescription).toBe('A moonlit tower');
  });

  it('ignores invalid description contexts and reports blob failures', () => {
    component.describeZoomedMedia();
    component.folder = folder('images');
    component.zoomedMedia = preview('video.mp4', 'blob:video', true);
    component.describeZoomedMedia();
    component.zoomedMedia = preview('image.png');
    mediaLibraryService.getMediaBlob.and.returnValue(
      throwError(() => new Error('failed')),
    );

    component.describeZoomedMedia();

    expect(mediaLibraryService.getMediaBlob).toHaveBeenCalledOnceWith(
      'images',
      'image.png',
    );
    expect(toastrService.error).toHaveBeenCalledOnceWith(
      'Failed to load image',
    );
  });

  it('configures thumbnail and overlay video playback', fakeAsync(() => {
    const thumbnail = document.createElement('video');
    const thumbnailPlay = spyOn(thumbnail, 'play').and.resolveTo();
    component.ensureVideoPlayback({ target: thumbnail } as unknown as Event);
    expect(thumbnail.defaultMuted).toBeTrue();
    expect(thumbnail.muted).toBeTrue();
    expect(thumbnail.playsInline).toBeTrue();
    expect(thumbnailPlay).toHaveBeenCalled();

    const overlay = document.createElement('video');
    const overlayPlay = spyOn(overlay, 'play').and.rejectWith(
      new Error('blocked'),
    );
    component.ensureOverlayVideoPlayback({
      target: overlay,
    } as unknown as Event);
    flushMicrotasks();
    expect(overlay.defaultMuted).toBeFalse();
    expect(overlay.muted).toBeFalse();
    expect(overlay.playsInline).toBeTrue();
    expect(overlayPlay).toHaveBeenCalled();
  }));

  it('ignores playback events from non-video targets', () => {
    component.ensureVideoPlayback({
      target: document.createElement('div'),
    } as unknown as Event);
    component.ensureOverlayVideoPlayback({
      target: document.createElement('div'),
    } as unknown as Event);

    expect().nothing();
  });

  it('closes dialogs, observers, subscriptions, and object URLs on destroy', () => {
    const blobResponse = new Subject<Blob>();
    mediaLibraryService.getMedia.and.returnValue(of([mediaFile('image.png')]));
    mediaLibraryService.getMediaBlob.and.returnValue(blobResponse);
    component.folder = folder('images');
    const ref = dialogRef<void>();
    dialogService.open.and.returnValue(ref);
    component.openAddMediaDialog();
    const content = document.createElement('div');
    component.mediaFolderContentRef = new ElementRef(content);

    component.ngOnDestroy();
    blobResponse.next(new Blob(['late'], { type: 'image/png' }));

    expect(ref.close).toHaveBeenCalledTimes(1);
    expect(disconnectSpy).toHaveBeenCalled();
    expect(createObjectUrlSpy).not.toHaveBeenCalled();
  });
});
