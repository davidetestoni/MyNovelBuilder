import { TestBed } from '@angular/core/testing';
import {
  DynamicDialogConfig,
  DynamicDialogRef,
} from 'primeng/dynamicdialog';
import {
  UploadMediaDialogComponent,
  UploadMediaDialogData,
} from './upload-media-dialog.component';

describe('UploadMediaDialogComponent', () => {
  let component: UploadMediaDialogComponent;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let config: { data?: UploadMediaDialogData };

  const initialFile = new File(['initial'], 'initial.png', {
    type: 'image/png',
  });

  const createComponent = (
    data?: UploadMediaDialogData,
  ): UploadMediaDialogComponent => {
    config.data = data;
    return TestBed.runInInjectionContext(
      () => new UploadMediaDialogComponent(),
    );
  };

  const fileInput = (file: File | null): HTMLInputElement => {
    const input = document.createElement('input');
    Object.defineProperty(input, 'files', {
      configurable: true,
      value: file === null ? [] : [file],
    });
    return input;
  };

  beforeEach(() => {
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>('DynamicDialogRef', [
      'close',
    ]);
    config = {};

    TestBed.configureTestingModule({
      providers: [
        { provide: DynamicDialogRef, useValue: dialogRef },
        { provide: DynamicDialogConfig, useValue: config },
      ],
    });

    component = createComponent();
  });

  it('starts empty when no dialog data is provided', () => {
    expect(component.data).toEqual({});
    expect(component.selectedFile).toBeNull();
    expect(component.formGroup.controls.name.value).toBe('');
    expect(component.formGroup.invalid).toBeTrue();
  });

  it('initializes the file and name from dialog data', () => {
    component = createComponent({
      initialFile,
      initialName: 'cover-image.png',
    });

    expect(component.selectedFile).toBe(initialFile);
    expect(component.formGroup.controls.name.value).toBe('cover-image.png');
    expect(component.formGroup.valid).toBeTrue();
  });

  it('accepts an initial file without inventing a media name', () => {
    component = createComponent({ initialFile });

    expect(component.selectedFile).toBe(initialFile);
    expect(component.formGroup.controls.name.value).toBe('');
    expect(component.formGroup.invalid).toBeTrue();
  });

  it('ignores file-selection events from non-input targets', () => {
    component.onFileSelected({ target: document.body } as unknown as Event);

    expect(component.selectedFile).toBeNull();
    expect(component.formGroup.controls.name.value).toBe('');
  });

  it('selects a file and uses its name when the media name is empty', () => {
    const selected = new File(['video'], 'intro.mp4', {
      type: 'video/mp4',
    });

    component.onFileSelected({
      target: fileInput(selected),
    } as unknown as Event);

    expect(component.selectedFile).toBe(selected);
    expect(component.formGroup.controls.name.value).toBe('intro.mp4');
    expect(component.formGroup.valid).toBeTrue();
  });

  it('uses a file name when the current media name contains only whitespace', () => {
    const selected = new File(['image'], 'portrait.webp', {
      type: 'image/webp',
    });
    component.formGroup.controls.name.setValue('   ');

    component.onFileSelected({
      target: fileInput(selected),
    } as unknown as Event);

    expect(component.formGroup.controls.name.value).toBe('portrait.webp');
  });

  it('preserves a custom media name when selecting a file', () => {
    const selected = new File(['image'], 'camera-name.png', {
      type: 'image/png',
    });
    component.formGroup.controls.name.setValue('custom-name.png');

    component.onFileSelected({
      target: fileInput(selected),
    } as unknown as Event);

    expect(component.selectedFile).toBe(selected);
    expect(component.formGroup.controls.name.value).toBe('custom-name.png');
  });

  it('clears the selected file when the file picker is emptied', () => {
    component = createComponent({
      initialFile,
      initialName: 'initial.png',
    });

    component.onFileSelected({
      target: fileInput(null),
    } as unknown as Event);

    expect(component.selectedFile).toBeNull();
    expect(component.formGroup.controls.name.value).toBe('initial.png');
  });

  it('does not submit without a file or with a blank name', () => {
    component.formGroup.controls.name.setValue('media.png');
    component.submit();

    component.selectedFile = initialFile;
    component.formGroup.controls.name.setValue('   ');
    component.submit();

    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('trims the media name and returns the selected file', () => {
    component.selectedFile = initialFile;
    component.formGroup.controls.name.setValue('  renamed-cover.png  ');

    component.submit();

    expect(dialogRef.close).toHaveBeenCalledOnceWith({
      name: 'renamed-cover.png',
      file: initialFile,
    });
  });
});
