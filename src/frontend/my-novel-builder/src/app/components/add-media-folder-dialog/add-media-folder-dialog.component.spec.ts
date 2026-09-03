import { TestBed } from '@angular/core/testing';
import { DynamicDialogRef } from 'primeng/dynamicdialog';
import { AddMediaFolderDialogComponent } from './add-media-folder-dialog.component';

describe('AddMediaFolderDialogComponent', () => {
  let component: AddMediaFolderDialogComponent;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;

  beforeEach(() => {
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>('DynamicDialogRef', [
      'close',
    ]);

    TestBed.configureTestingModule({
      providers: [{ provide: DynamicDialogRef, useValue: dialogRef }],
    });

    component = TestBed.runInInjectionContext(
      () => new AddMediaFolderDialogComponent(),
    );
  });

  it('starts with an invalid empty form', () => {
    expect(component.formGroup.getRawValue()).toEqual({
      name: '',
      path: '',
    });
    expect(component.formGroup.invalid).toBeTrue();
  });

  it('requires both the folder name and path', () => {
    component.formGroup.patchValue({ name: 'References' });
    expect(component.formGroup.invalid).toBeTrue();

    component.formGroup.setValue({
      name: '',
      path: '/media/references',
    });
    expect(component.formGroup.invalid).toBeTrue();

    component.formGroup.setValue({
      name: 'References',
      path: '/media/references',
    });
    expect(component.formGroup.valid).toBeTrue();
  });

  it('does not submit whitespace-only values', () => {
    component.formGroup.setValue({
      name: '   ',
      path: ' /media/references ',
    });
    component.submit();

    component.formGroup.setValue({
      name: ' References ',
      path: '   ',
    });
    component.submit();

    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('trims the folder values before returning them', () => {
    component.formGroup.setValue({
      name: '  Reference Images  ',
      path: '  /media/reference-images  ',
    });

    component.submit();

    expect(dialogRef.close).toHaveBeenCalledOnceWith({
      name: 'Reference Images',
      path: '/media/reference-images',
    });
  });
});
