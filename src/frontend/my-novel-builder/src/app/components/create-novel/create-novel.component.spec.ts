import { TestBed } from '@angular/core/testing';
import { DynamicDialogRef } from 'primeng/dynamicdialog';
import { ToastrService } from 'ngx-toastr';
import { of, Subject, throwError } from 'rxjs';
import { NovelService } from '../../services/novel.service';
import type { NovelDto } from '../../types/dtos/novel/novel.dto';
import { WritingLanguage } from '../../types/enums/writing-language';
import { WritingPov } from '../../types/enums/writing-pov';
import { WritingTense } from '../../types/enums/writing-tense';
import { CreateNovelComponent } from './create-novel.component';

describe('CreateNovelComponent workflow', () => {
  let component: CreateNovelComponent;
  let novelService: jasmine.SpyObj<NovelService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let toastr: jasmine.SpyObj<ToastrService>;
  let fileReader: jasmine.SpyObj<FileReader>;
  let originalClipboardDescriptor: PropertyDescriptor | undefined;

  const createdNovel = (): NovelDto => ({
    id: 'created-novel',
    createdAt: '2026-07-29T12:00:00Z',
    updatedAt: '2026-07-29T12:00:00Z',
    title: 'Created novel',
    author: '',
    brief: '',
    coverImageUrl: null,
    tense: WritingTense.Present,
    pov: WritingPov.FirstPerson,
    language: WritingLanguage.English,
    rpgMode: false,
    mainCharacterId: null,
    compendiumIds: [],
  });

  const setValidForm = (): void => {
    component.formGroup.setValue({
      title: 'The Novel',
      author: 'The Author',
      brief: 'The brief',
      tense: WritingTense.Past,
      pov: WritingPov.ThirdPersonLimited,
      language: WritingLanguage.Italian,
    });
  };

  const setClipboard = (read: jasmine.Spy): void => {
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { read },
    });
  };

  beforeEach(() => {
    originalClipboardDescriptor = Object.getOwnPropertyDescriptor(
      navigator,
      'clipboard',
    );
    novelService = jasmine.createSpyObj<NovelService>('NovelService', [
      'createNovel',
      'uploadNovelCoverImage',
    ]);
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>('DynamicDialogRef', [
      'close',
    ]);
    toastr = jasmine.createSpyObj<ToastrService>('ToastrService', [
      'success',
      'error',
    ]);
    fileReader = jasmine.createSpyObj<FileReader>('FileReader', [
      'readAsDataURL',
    ]);
    fileReader.readAsDataURL.and.callFake(() => {
      Object.defineProperty(fileReader, 'result', {
        configurable: true,
        value: 'data:image/png;base64,Y292ZXI=',
      });
      fileReader.onload?.({
        target: fileReader,
      } as unknown as ProgressEvent<FileReader>);
    });
    spyOn(window, 'FileReader').and.returnValue(fileReader);

    novelService.createNovel.and.returnValue(of(createdNovel()));
    novelService.uploadNovelCoverImage.and.returnValue(of(undefined));

    TestBed.configureTestingModule({
      providers: [
        { provide: NovelService, useValue: novelService },
        { provide: DynamicDialogRef, useValue: dialogRef },
        { provide: ToastrService, useValue: toastr },
      ],
    });

    component = TestBed.runInInjectionContext(
      () => new CreateNovelComponent(),
    );
  });

  afterEach(() => {
    if (originalClipboardDescriptor === undefined) {
      delete (navigator as unknown as { clipboard?: Clipboard }).clipboard;
    } else {
      Object.defineProperty(
        navigator,
        'clipboard',
        originalClipboardDescriptor,
      );
    }
  });

  it('starts with the intended writing defaults and a required title', () => {
    expect(component.formGroup.value).toEqual({
      title: '',
      author: '',
      brief: '',
      tense: WritingTense.Present,
      pov: WritingPov.FirstPerson,
      language: WritingLanguage.English,
    });
    expect(component.formGroup.invalid).toBeTrue();
    expect(component.formGroup.get('title')?.hasError('required')).toBeTrue();
  });

  it('enforces the text length limits', () => {
    component.formGroup.patchValue({
      title: 't'.repeat(101),
      author: 'a'.repeat(101),
      brief: 'b'.repeat(501),
    });

    expect(component.formGroup.get('title')?.hasError('maxlength')).toBeTrue();
    expect(component.formGroup.get('author')?.hasError('maxlength')).toBeTrue();
    expect(component.formGroup.get('brief')?.hasError('maxlength')).toBeTrue();
  });

  it('rejects unsupported writing settings', () => {
    component.formGroup.patchValue({
      title: 'Valid title',
      tense: 'future' as WritingTense,
      pov: 'secondPerson' as WritingPov,
      language: 'klingon' as WritingLanguage,
    });

    expect(component.formGroup.get('tense')?.hasError('pattern')).toBeTrue();
    expect(component.formGroup.get('pov')?.hasError('pattern')).toBeTrue();
    expect(component.formGroup.get('language')?.hasError('pattern')).toBeTrue();
    expect(component.formGroup.invalid).toBeTrue();
  });

  it('does not submit an invalid form', () => {
    component.createNovel();

    expect(novelService.createNovel).not.toHaveBeenCalled();
    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('maps the form into the create DTO and closes on success', () => {
    setValidForm();

    component.createNovel();

    expect(novelService.createNovel).toHaveBeenCalledOnceWith({
      title: 'The Novel',
      author: 'The Author',
      brief: 'The brief',
      tense: WritingTense.Past,
      pov: WritingPov.ThirdPersonLimited,
      language: WritingLanguage.Italian,
      rpgMode: false,
      mainCharacterId: null,
    });
    expect(novelService.uploadNovelCoverImage).not.toHaveBeenCalled();
    expect(toastr.success).toHaveBeenCalledOnceWith(
      'Novel created successfully.',
    );
    expect(dialogRef.close).toHaveBeenCalledOnceWith(true);
    expect(component.isCreating).toBeFalse();
  });

  it('normalizes nullable optional text fields to empty strings', () => {
    setValidForm();
    component.formGroup.patchValue({ author: null, brief: null });

    component.createNovel();

    expect(novelService.createNovel).toHaveBeenCalledOnceWith(
      jasmine.objectContaining({
        author: '',
        brief: '',
      }),
    );
  });

  it('uploads the selected cover before reporting success', () => {
    const uploadResponse = new Subject<void>();
    const cover = new File(['cover'], 'cover.png', { type: 'image/png' });
    setValidForm();
    component.imageFile = cover;
    novelService.uploadNovelCoverImage.and.returnValue(uploadResponse);

    component.createNovel();

    expect(novelService.uploadNovelCoverImage).toHaveBeenCalledOnceWith(
      'created-novel',
      cover,
    );
    expect(dialogRef.close).not.toHaveBeenCalled();

    uploadResponse.next();
    expect(toastr.success).toHaveBeenCalledOnceWith(
      'Novel created successfully.',
    );
    expect(dialogRef.close).toHaveBeenCalledOnceWith(true);

    uploadResponse.complete();
    expect(component.isCreating).toBeFalse();
  });

  it('prevents duplicate creation while a request is pending', () => {
    const createResponse = new Subject<NovelDto>();
    setValidForm();
    novelService.createNovel.and.returnValue(createResponse);

    component.createNovel();
    component.createNovel();

    expect(component.isCreating).toBeTrue();
    expect(novelService.createNovel).toHaveBeenCalledTimes(1);

    createResponse.next(createdNovel());
    createResponse.complete();
    expect(component.isCreating).toBeFalse();
  });

  it('restores creation and reports an error when creation fails', () => {
    setValidForm();
    novelService.createNovel.and.returnValue(
      throwError(() => new Error('request failed')),
    );

    component.createNovel();

    expect(component.isCreating).toBeFalse();
    expect(toastr.error).toHaveBeenCalledOnceWith('Failed to create novel.');
    expect(toastr.success).not.toHaveBeenCalled();
    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('restores creation and reports an error when cover upload fails', () => {
    setValidForm();
    component.imageFile = new File(['cover'], 'cover.png', {
      type: 'image/png',
    });
    novelService.uploadNovelCoverImage.and.returnValue(
      throwError(() => new Error('upload failed')),
    );

    component.createNovel();

    expect(component.isCreating).toBeFalse();
    expect(toastr.error).toHaveBeenCalledOnceWith('Failed to create novel.');
    expect(toastr.success).not.toHaveBeenCalled();
    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('ignores cover-change events without a selected file', () => {
    component.onCoverChange({
      target: { files: null },
    } as unknown as Event);

    expect(component.imageFile).toBeNull();
    expect(fileReader.readAsDataURL).not.toHaveBeenCalled();
  });

  it('stores and previews a cover selected from the file input', () => {
    const cover = new File(['cover'], 'cover.png', { type: 'image/png' });

    component.onCoverChange({
      target: { files: [cover] },
    } as unknown as Event);

    expect(component.imageFile).toBe(cover);
    expect(fileReader.readAsDataURL).toHaveBeenCalledOnceWith(cover);
    expect(component.imagePreview).toBe(
      'data:image/png;base64,Y292ZXI=',
    );
  });

  it('stores and previews an image read from the clipboard', async () => {
    const blob = new Blob(['cover'], { type: 'image/jpeg' });
    const read = jasmine
      .createSpy('read')
      .and.resolveTo([
        {
          types: ['text/plain', 'image/jpeg'],
          getType: jasmine.createSpy('getType').and.resolveTo(blob),
        },
      ]);
    setClipboard(read);

    await component.readImageFromClipboard();

    expect(read).toHaveBeenCalledTimes(1);
    expect(component.imageFile).toEqual(
      jasmine.objectContaining({
        name: 'clipboard-image.jpg',
        type: 'image/jpeg',
      }),
    );
    expect(fileReader.readAsDataURL).toHaveBeenCalledOnceWith(
      component.imageFile!,
    );
    expect(component.imagePreview).toBe(
      'data:image/png;base64,Y292ZXI=',
    );
  });

  it('shows the clipboard error message when no image can be read', async () => {
    setClipboard(
      jasmine
        .createSpy('read')
        .and.resolveTo([{ types: [], getType: jasmine.createSpy('getType') }]),
    );

    await component.readImageFromClipboard();

    expect(toastr.error).toHaveBeenCalledOnceWith(
      'No image found in the clipboard.',
    );
    expect(component.imageFile).toBeNull();
  });

  it('uses a safe fallback for non-Error clipboard failures', async () => {
    setClipboard(jasmine.createSpy('read').and.rejectWith('permission denied'));

    await component.readImageFromClipboard();

    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Failed to read image from clipboard.',
    );
  });
});
