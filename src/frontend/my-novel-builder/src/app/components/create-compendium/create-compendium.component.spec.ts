import { TestBed } from '@angular/core/testing';
import { DynamicDialogRef } from 'primeng/dynamicdialog';
import { ToastrService } from 'ngx-toastr';
import { of, Subject, throwError } from 'rxjs';
import { CompendiumService } from '../../services/compendium.service';
import type { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import { CreateCompendiumComponent } from './create-compendium.component';

describe('CreateCompendiumComponent workflow', () => {
  let component: CreateCompendiumComponent;
  let compendiumService: jasmine.SpyObj<CompendiumService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let toastr: jasmine.SpyObj<ToastrService>;

  const createdCompendium = (): CompendiumDto => ({
    id: 'created',
    createdAt: '2026-07-29T12:00:00Z',
    updatedAt: '2026-07-29T12:00:00Z',
    name: 'World',
    description: 'A world',
    records: [],
  });

  beforeEach(() => {
    compendiumService = jasmine.createSpyObj<CompendiumService>(
      'CompendiumService',
      ['createCompendium'],
    );
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>('DynamicDialogRef', [
      'close',
    ]);
    toastr = jasmine.createSpyObj<ToastrService>('ToastrService', [
      'success',
      'error',
    ]);
    compendiumService.createCompendium.and.returnValue(of(createdCompendium()));

    TestBed.configureTestingModule({
      providers: [
        { provide: CompendiumService, useValue: compendiumService },
        { provide: DynamicDialogRef, useValue: dialogRef },
        { provide: ToastrService, useValue: toastr },
      ],
    });

    component = TestBed.runInInjectionContext(
      () => new CreateCompendiumComponent(),
    );
  });

  it('starts with an empty form and a required name', () => {
    expect(component.formGroup.value).toEqual({
      name: '',
      description: '',
    });
    expect(component.formGroup.invalid).toBeTrue();
    expect(component.formGroup.get('name')?.hasError('required')).toBeTrue();
  });

  it('enforces name and description length limits', () => {
    component.formGroup.setValue({
      name: 'n'.repeat(101),
      description: 'd'.repeat(501),
    });

    expect(component.formGroup.get('name')?.hasError('maxlength')).toBeTrue();
    expect(
      component.formGroup.get('description')?.hasError('maxlength'),
    ).toBeTrue();
  });

  it('does not submit an invalid form', () => {
    component.createCompendium();

    expect(compendiumService.createCompendium).not.toHaveBeenCalled();
    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('maps the form and closes with a success result', () => {
    component.formGroup.setValue({
      name: 'World',
      description: 'A world',
    });

    component.createCompendium();

    expect(compendiumService.createCompendium).toHaveBeenCalledOnceWith({
      name: 'World',
      description: 'A world',
    });
    expect(toastr.success).toHaveBeenCalledOnceWith(
      'Compendium created successfully.',
    );
    expect(dialogRef.close).toHaveBeenCalledOnceWith(true);
    expect(component.isCreating).toBeFalse();
  });

  it('normalizes a nullable description to an empty string', () => {
    component.formGroup.setValue({
      name: 'World',
      description: null,
    });

    component.createCompendium();

    expect(compendiumService.createCompendium).toHaveBeenCalledOnceWith({
      name: 'World',
      description: '',
    });
  });

  it('prevents duplicate creation while a request is pending', () => {
    const response = new Subject<CompendiumDto>();
    component.formGroup.patchValue({ name: 'World' });
    compendiumService.createCompendium.and.returnValue(response);

    component.createCompendium();
    component.createCompendium();

    expect(component.isCreating).toBeTrue();
    expect(compendiumService.createCompendium).toHaveBeenCalledTimes(1);

    response.next(createdCompendium());
    response.complete();
    expect(component.isCreating).toBeFalse();
  });

  it('reports failure and restores creation so the user can retry', () => {
    component.formGroup.patchValue({ name: 'World' });
    compendiumService.createCompendium.and.returnValue(
      throwError(() => new Error('request failed')),
    );

    component.createCompendium();

    expect(component.isCreating).toBeFalse();
    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Failed to create compendium.',
    );
    expect(toastr.success).not.toHaveBeenCalled();
    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('closes without a result when cancelled', () => {
    component.cancel();

    expect(dialogRef.close).toHaveBeenCalledOnceWith();
    expect(compendiumService.createCompendium).not.toHaveBeenCalled();
  });
});
