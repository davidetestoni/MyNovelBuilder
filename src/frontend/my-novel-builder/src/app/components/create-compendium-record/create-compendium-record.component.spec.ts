import { TestBed } from '@angular/core/testing';
import {
  DialogService,
  DynamicDialogConfig,
  DynamicDialogRef,
} from 'primeng/dynamicdialog';
import { ToastrService } from 'ngx-toastr';
import { of, Subject, throwError } from 'rxjs';
import { CompendiumService } from '../../services/compendium.service';
import type { CompendiumRecordDto } from '../../types/dtos/compendium-record/compendium-record.dto';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { PromptType } from '../../types/enums/prompt-type';
import { DescribeImageComponent } from '../describe-image/describe-image.component';
import { CreateCompendiumRecordComponent } from './create-compendium-record.component';

describe('CreateCompendiumRecordComponent workflow', () => {
  let component: CreateCompendiumRecordComponent;
  let compendiumService: jasmine.SpyObj<CompendiumService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let describeDialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let dialogService: jasmine.SpyObj<DialogService>;
  let toastr: jasmine.SpyObj<ToastrService>;
  let descriptionClosed: Subject<string | undefined>;
  let fileReader: jasmine.SpyObj<FileReader>;
  let originalClipboardDescriptor: PropertyDescriptor | undefined;

  const createdRecord = (): CompendiumRecordDto => ({
    id: 'created-record',
    createdAt: '2026-07-29T12:00:00Z',
    updatedAt: '2026-07-29T12:00:00Z',
    name: 'Aria',
    aliases: 'The Hero',
    type: CompendiumRecordType.Character,
    context: 'A hero',
    contextTokenCount: 2,
    media: [],
    compendiumId: 'compendium-id',
    alwaysIncluded: true,
    characterVoiceAssignments: [],
  });

  const setValidForm = (): void => {
    component.formGroup.setValue({
      name: 'Aria',
      aliases: 'The Hero',
      type: CompendiumRecordType.Character,
      context: 'A hero',
      alwaysIncluded: true,
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
    compendiumService = jasmine.createSpyObj<CompendiumService>(
      'CompendiumService',
      ['createRecord', 'uploadRecordMedia'],
    );
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>('DynamicDialogRef', [
      'close',
    ]);
    descriptionClosed = new Subject<string | undefined>();
    describeDialogRef = jasmine.createSpyObj<DynamicDialogRef>(
      'DescribeDialogRef',
      ['close'],
      { onClose: descriptionClosed.asObservable() },
    );
    dialogService = jasmine.createSpyObj<DialogService>('DialogService', [
      'open',
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
        value: 'data:image/png;base64,aW1hZ2U=',
      });
      fileReader.onload?.({
        target: fileReader,
      } as unknown as ProgressEvent<FileReader>);
    });
    spyOn(window, 'FileReader').and.returnValue(fileReader);

    compendiumService.createRecord.and.returnValue(of(createdRecord()));
    compendiumService.uploadRecordMedia.and.returnValue(of(undefined));
    dialogService.open.and.returnValue(describeDialogRef);

    TestBed.configureTestingModule({
      providers: [
        { provide: CompendiumService, useValue: compendiumService },
        { provide: DynamicDialogRef, useValue: dialogRef },
        {
          provide: DynamicDialogConfig,
          useValue: { data: { compendiumId: 'compendium-id' } },
        },
        { provide: DialogService, useValue: dialogService },
        { provide: ToastrService, useValue: toastr },
      ],
    });

    component = TestBed.runInInjectionContext(
      () => new CreateCompendiumRecordComponent(),
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

  it('starts with the intended defaults and supported record types', () => {
    expect(component.formGroup.value).toEqual({
      name: '',
      aliases: '',
      type: CompendiumRecordType.Character,
      context: '',
      alwaysIncluded: false,
    });
    expect(component.recordTypes).toEqual(Object.values(CompendiumRecordType));
    expect(component.formGroup.invalid).toBeTrue();
  });

  it('enforces text limits and supported record types', () => {
    component.formGroup.setValue({
      name: 'n'.repeat(101),
      aliases: 'a'.repeat(501),
      type: 'unsupported' as CompendiumRecordType,
      context: 'c'.repeat(10001),
      alwaysIncluded: false,
    });

    expect(component.formGroup.get('name')?.hasError('maxlength')).toBeTrue();
    expect(component.formGroup.get('aliases')?.hasError('maxlength')).toBeTrue();
    expect(component.formGroup.get('type')?.hasError('pattern')).toBeTrue();
    expect(component.formGroup.get('context')?.hasError('maxlength')).toBeTrue();
  });

  it('adds a trimmed alias to the current comma-separated list', () => {
    component.formGroup.get('aliases')?.setValue(' First, Second ');

    component.addAlias('Third');

    expect(component.formGroup.get('aliases')?.value).toBe(
      'First, Second, Third',
    );
    expect(component.formGroup.get('aliases')?.dirty).toBeTrue();
  });

  it('does not add a duplicate alias regardless of case', () => {
    component.formGroup.get('aliases')?.setValue('Hero, Friend');
    component.formGroup.get('aliases')?.markAsPristine();

    component.addAlias('hErO');

    expect(component.formGroup.get('aliases')?.value).toBe('Hero, Friend');
    expect(component.formGroup.get('aliases')?.pristine).toBeTrue();
  });

  it('does not submit an invalid form', () => {
    component.createRecord();

    expect(compendiumService.createRecord).not.toHaveBeenCalled();
    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('maps the form and closes with the created record', () => {
    const record = createdRecord();
    setValidForm();
    compendiumService.createRecord.and.returnValue(of(record));

    component.createRecord();

    expect(compendiumService.createRecord).toHaveBeenCalledOnceWith({
      name: 'Aria',
      aliases: 'The Hero',
      type: CompendiumRecordType.Character,
      context: 'A hero',
      compendiumId: 'compendium-id',
      alwaysIncluded: true,
      characterVoiceAssignments: [],
    });
    expect(compendiumService.uploadRecordMedia).not.toHaveBeenCalled();
    expect(toastr.success).toHaveBeenCalledOnceWith(
      'Record created successfully',
    );
    expect(dialogRef.close).toHaveBeenCalledOnceWith(record);
    expect(component.isCreating).toBeFalse();
  });

  it('normalizes nullable optional values', () => {
    setValidForm();
    component.formGroup.patchValue({
      aliases: null,
      context: null,
      alwaysIncluded: null,
    });

    component.createRecord();

    expect(compendiumService.createRecord).toHaveBeenCalledOnceWith(
      jasmine.objectContaining({
        aliases: '',
        context: '',
        alwaysIncluded: false,
      }),
    );
  });

  it('uploads a selected image before closing with the created record', () => {
    const uploadResponse = new Subject<void>();
    const image = new File(['image'], 'record.png', { type: 'image/png' });
    const record = createdRecord();
    setValidForm();
    component.imageFile = image;
    compendiumService.createRecord.and.returnValue(of(record));
    compendiumService.uploadRecordMedia.and.returnValue(uploadResponse);

    component.createRecord();

    expect(compendiumService.uploadRecordMedia).toHaveBeenCalledOnceWith(
      'created-record',
      image,
      true,
    );
    expect(dialogRef.close).not.toHaveBeenCalled();

    uploadResponse.next();
    expect(dialogRef.close).toHaveBeenCalledOnceWith(record);
    expect(toastr.success).toHaveBeenCalledOnceWith(
      'Record created successfully',
    );

    uploadResponse.complete();
    expect(component.isCreating).toBeFalse();
  });

  it('prevents duplicate creation while a request is pending', () => {
    const response = new Subject<CompendiumRecordDto>();
    setValidForm();
    compendiumService.createRecord.and.returnValue(response);

    component.createRecord();
    component.createRecord();

    expect(component.isCreating).toBeTrue();
    expect(compendiumService.createRecord).toHaveBeenCalledTimes(1);

    response.next(createdRecord());
    response.complete();
    expect(component.isCreating).toBeFalse();
  });

  it('reports creation failure and restores retry state', () => {
    setValidForm();
    compendiumService.createRecord.and.returnValue(
      throwError(() => new Error('request failed')),
    );

    component.createRecord();

    expect(component.isCreating).toBeFalse();
    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Failed to create record.',
    );
    expect(toastr.success).not.toHaveBeenCalled();
    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('reports upload failure and restores retry state', () => {
    setValidForm();
    component.imageFile = new File(['image'], 'record.png', {
      type: 'image/png',
    });
    compendiumService.uploadRecordMedia.and.returnValue(
      throwError(() => new Error('upload failed')),
    );

    component.createRecord();

    expect(component.isCreating).toBeFalse();
    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Failed to create record.',
    );
    expect(toastr.success).not.toHaveBeenCalled();
    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('ignores image-change events without a selected file', () => {
    component.onImageChange({
      target: { files: null },
    } as unknown as Event);

    expect(component.imageFile).toBeNull();
    expect(fileReader.readAsDataURL).not.toHaveBeenCalled();
  });

  it('stores and previews an image selected from the file input', () => {
    const image = new File(['image'], 'record.png', { type: 'image/png' });

    component.onImageChange({
      target: { files: [image] },
    } as unknown as Event);

    expect(component.imageFile).toBe(image);
    expect(fileReader.readAsDataURL).toHaveBeenCalledOnceWith(image);
    expect(component.imagePreview).toBe(
      'data:image/png;base64,aW1hZ2U=',
    );
  });

  it('stores and previews an image read from the clipboard', async () => {
    const blob = new Blob(['image'], { type: 'image/webp' });
    const read = jasmine
      .createSpy('read')
      .and.resolveTo([
        {
          types: ['image/webp'],
          getType: jasmine.createSpy('getType').and.resolveTo(blob),
        },
      ]);
    setClipboard(read);

    await component.readImageFromClipboard();

    expect(component.imageFile).toEqual(
      jasmine.objectContaining({
        name: 'clipboard-image.webp',
        type: 'image/webp',
      }),
    );
    expect(fileReader.readAsDataURL).toHaveBeenCalledOnceWith(
      component.imageFile!,
    );
  });

  it('shows a useful clipboard error when no image is available', async () => {
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

  it('does not open image description without an image', () => {
    component.generateContextFromImage();

    expect(dialogService.open).not.toHaveBeenCalled();
  });

  it('opens image description with the record context', () => {
    const image = new File(['image'], 'record.png', { type: 'image/png' });
    component.imageFile = image;

    component.generateContextFromImage();

    expect(dialogService.open).toHaveBeenCalledOnceWith(
      DescribeImageComponent,
      {
        header: 'Describe Image',
        width: '70vw',
        contentStyle: { overflow: 'auto' },
        baseZIndex: 10000,
        closable: true,
        closeOnEscape: true,
        modal: true,
        dismissableMask: true,
        data: {
          image,
          compendiumId: 'compendium-id',
          promptType: PromptType.DescribeCompendiumImage,
        },
      },
    );
  });

  it('appends a trimmed image description to existing context', () => {
    component.imageFile = new File(['image'], 'record.png', {
      type: 'image/png',
    });
    component.formGroup.get('context')?.setValue(' Existing context ');
    component.formGroup.get('context')?.markAsPristine();
    component.formGroup.get('context')?.markAsUntouched();
    component.generateContextFromImage();

    descriptionClosed.next('  Generated description  ');

    const context = component.formGroup.get('context')!;
    expect(context.value).toBe(
      'Existing context\n\nGenerated description',
    );
    expect(context.dirty).toBeTrue();
    expect(context.touched).toBeTrue();
  });

  it('uses a generated description as the initial context', () => {
    component.imageFile = new File(['image'], 'record.png', {
      type: 'image/png',
    });
    component.generateContextFromImage();

    descriptionClosed.next('  Generated description  ');

    expect(component.formGroup.get('context')?.value).toBe(
      'Generated description',
    );
  });

  it('ignores empty image descriptions', () => {
    component.imageFile = new File(['image'], 'record.png', {
      type: 'image/png',
    });
    component.formGroup.get('context')?.setValue('Existing');
    component.generateContextFromImage();

    descriptionClosed.next('   ');
    descriptionClosed.next(undefined);

    expect(component.formGroup.get('context')?.value).toBe('Existing');
  });

  it('closes an open image-description dialog when destroyed', () => {
    component.imageFile = new File(['image'], 'record.png', {
      type: 'image/png',
    });
    component.generateContextFromImage();

    component.ngOnDestroy();

    expect(describeDialogRef.close).toHaveBeenCalledTimes(1);
  });

  it('can be destroyed without opening an image-description dialog', () => {
    expect(() => component.ngOnDestroy()).not.toThrow();
    expect(describeDialogRef.close).not.toHaveBeenCalled();
  });
});
