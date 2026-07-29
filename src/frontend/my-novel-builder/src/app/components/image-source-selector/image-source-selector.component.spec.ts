import { TestBed } from '@angular/core/testing';
import {
  DynamicDialogConfig,
  DynamicDialogRef,
} from 'primeng/dynamicdialog';
import {
  ImageSourceSelectorComponent,
  ImageSourceSelectorComponentData,
} from './image-source-selector.component';

describe('ImageSourceSelectorComponent', () => {
  let component: ImageSourceSelectorComponent;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let config: { data?: ImageSourceSelectorComponentData };

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

    component = TestBed.runInInjectionContext(
      () => new ImageSourceSelectorComponent(),
    );
  });

  it('provides clear default labels', () => {
    expect(component.uploadLabel).toBe('Upload Image');
    expect(component.generateLabel).toBe('Generate Media');
    expect(component.clipboardLabel).toBe('Paste from Clipboard');
  });

  it('uses labels supplied by the caller', () => {
    config.data = {
      uploadLabel: 'Choose File',
      generateLabel: 'Create Image',
      clipboardLabel: 'Paste Image',
    };

    expect(component.uploadLabel).toBe('Choose File');
    expect(component.generateLabel).toBe('Create Image');
    expect(component.clipboardLabel).toBe('Paste Image');
  });

  it('closes with each selected source', () => {
    component.select('upload');
    component.select('generate');
    component.select('clipboard');

    expect(dialogRef.close.calls.allArgs()).toEqual([
      ['upload'],
      ['generate'],
      ['clipboard'],
    ]);
  });
});
