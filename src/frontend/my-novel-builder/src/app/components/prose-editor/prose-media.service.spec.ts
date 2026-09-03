import { TestBed } from '@angular/core/testing';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { Subject, of } from 'rxjs';
import { NovelService } from '../../services/novel.service';
import { GenerateMediaComponent } from '../generate-media/generate-media.component';
import { ImageSourceSelectorComponent } from '../image-source-selector/image-source-selector.component';
import { ProseMediaService, ProseMediaSource } from './prose-media.service';

describe('ProseMediaService', () => {
  let service: ProseMediaService;
  let dialogService: jasmine.SpyObj<DialogService>;
  let novelService: jasmine.SpyObj<NovelService>;

  const originalClipboardDescriptor = Object.getOwnPropertyDescriptor(
    navigator,
    'clipboard',
  );

  const createDialogRef = <T>(onClose = new Subject<T>()): DynamicDialogRef =>
    ({
      onClose,
      close: jasmine.createSpy('close'),
    }) as unknown as DynamicDialogRef;

  beforeEach(() => {
    dialogService = jasmine.createSpyObj<DialogService>('DialogService', [
      'open',
    ]);
    novelService = jasmine.createSpyObj<NovelService>('NovelService', [
      'deleteProseImage',
      'uploadProseImage',
    ]);

    TestBed.configureTestingModule({
      providers: [
        { provide: DialogService, useValue: dialogService },
        { provide: NovelService, useValue: novelService },
      ],
    });

    service = TestBed.runInInjectionContext(() => new ProseMediaService());
  });

  afterEach(() => {
    if (originalClipboardDescriptor === undefined) {
      delete (navigator as { clipboard?: Clipboard }).clipboard;
      return;
    }

    Object.defineProperty(
      navigator,
      'clipboard',
      originalClipboardDescriptor,
    );
  });

  it('opens the source selector and returns the selected source', () => {
    const close$ = new Subject<ProseMediaSource>();
    dialogService.open.and.returnValue(createDialogRef(close$));
    let selected: ProseMediaSource | undefined;

    service.selectSource().subscribe((source) => (selected = source));
    close$.next('clipboard');

    expect(dialogService.open).toHaveBeenCalledOnceWith(
      ImageSourceSelectorComponent,
      jasmine.objectContaining({ header: 'Add Image', modal: true }),
    );
    expect(selected).toBe('clipboard');
  });

  it('selects a local file, uploads it, and removes the temporary input', () => {
    const input = document.createElement('input');
    const file = new File(['image'], 'scene.png', { type: 'image/png' });
    Object.defineProperty(input, 'files', { value: [file] });
    spyOn(input, 'click');
    spyOn(input, 'remove');
    spyOn(document, 'createElement').and.returnValue(input);
    novelService.uploadProseImage.and.returnValue(of('uploaded.png'));
    let location: string | undefined;

    service
      .selectFileAndUpload('novel-1')
      .subscribe((value) => (location = value));
    input.onchange?.(new Event('change'));

    expect(input.type).toBe('file');
    expect(input.accept).toBe('image/*,video/*');
    expect(input.click).toHaveBeenCalled();
    expect(novelService.uploadProseImage).toHaveBeenCalledOnceWith(
      'novel-1',
      file,
    );
    expect(location).toBe('uploaded.png');
    expect(input.remove).toHaveBeenCalledTimes(1);
  });

  it('reads and uploads an image from the clipboard', async () => {
    const image = new Blob(['pixels'], { type: 'image/webp' });
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: {
        read: jasmine.createSpy('read').and.resolveTo([
          {
            types: ['image/webp'],
            getType: jasmine.createSpy('getType').and.resolveTo(image),
          },
        ]),
      },
    });
    novelService.uploadProseImage.and.returnValue(of('clipboard.webp'));

    const location = await service.uploadClipboardImage('novel-1');

    const uploadedFile = novelService.uploadProseImage.calls.mostRecent()
      .args[1];
    expect(uploadedFile.name).toBe('clipboard-image.webp');
    expect(uploadedFile.type).toBe('image/webp');
    expect(location).toBe('clipboard.webp');
  });

  it('uploads generated media with a filename derived from its MIME type', () => {
    const close$ = new Subject<Blob>();
    dialogService.open.and.returnValue(createDialogRef(close$));
    novelService.uploadProseImage.and.returnValue(of('generated.mp4'));
    let location: string | undefined;

    service
      .generateAndUpload('novel-1')
      .subscribe((value) => (location = value));
    close$.next(new Blob(['video'], { type: 'video/mp4' }));

    expect(dialogService.open.calls.mostRecent().args[0]).toBe(
      GenerateMediaComponent,
    );
    const uploadedFile = novelService.uploadProseImage.calls.mostRecent()
      .args[1];
    expect(uploadedFile.name).toBe('generated-media.mp4');
    expect(uploadedFile.type).toBe('video/mp4');
    expect(location).toBe('generated.mp4');
  });

  it('delegates deletion and closes the active dialog on destroy', () => {
    const ref = createDialogRef<ProseMediaSource>();
    dialogService.open.and.returnValue(ref);
    novelService.deleteProseImage.and.returnValue(of(undefined));

    service.selectSource();
    service.deleteImage('novel-1', 'image.png').subscribe();
    service.ngOnDestroy();

    expect(novelService.deleteProseImage).toHaveBeenCalledOnceWith(
      'novel-1',
      'image.png',
    );
    expect(ref.close).toHaveBeenCalledTimes(1);
  });
});
