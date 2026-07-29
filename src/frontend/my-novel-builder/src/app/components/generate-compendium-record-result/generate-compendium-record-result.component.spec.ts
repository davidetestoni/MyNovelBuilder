import { TestBed } from '@angular/core/testing';
import { ToastrService } from 'ngx-toastr';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { of, Subject, throwError } from 'rxjs';
import { CompendiumService } from '../../services/compendium.service';
import { NovelService } from '../../services/novel.service';
import { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import { CompendiumRecordDto } from '../../types/dtos/compendium-record/compendium-record.dto';
import { NovelDto } from '../../types/dtos/novel/novel.dto';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import {
  GenerateCompendiumRecordComponentData,
  GenerateCompendiumRecordResultComponent,
} from './generate-compendium-record-result.component';

describe('GenerateCompendiumRecordResultComponent workflow', () => {
  let component: GenerateCompendiumRecordResultComponent;
  let compendiumService: jasmine.SpyObj<CompendiumService>;
  let novelService: jasmine.SpyObj<NovelService>;
  let toastr: jasmine.SpyObj<ToastrService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let config: { data: GenerateCompendiumRecordComponentData };

  const compendium = (id: string): CompendiumDto => ({
    id,
    createdAt: '',
    updatedAt: '',
    name: `Compendium ${id}`,
    description: '',
    records: [],
  });

  const novel = (compendiumIds = ['linked']): NovelDto =>
    ({ id: 'novel-one', compendiumIds }) as NovelDto;

  const createdRecord = (): CompendiumRecordDto => ({
    id: 'record-one',
    createdAt: '',
    updatedAt: '',
    name: 'Aria',
    aliases: 'Hero',
    type: CompendiumRecordType.Character,
    context: 'Generated context',
    contextTokenCount: 2,
    media: [],
    compendiumId: 'linked',
    alwaysIncluded: false,
    characterVoiceAssignments: [],
  });

  const setValidForm = (): void => {
    component.formGroup.setValue({
      compendiumId: 'linked',
      type: CompendiumRecordType.Character,
      name: '  Aria  ',
      aliases: '  Hero  ',
      context: 'Generated context',
    });
  };

  beforeEach(() => {
    compendiumService = jasmine.createSpyObj<CompendiumService>(
      'CompendiumService',
      ['getCompendia', 'createRecord'],
    );
    novelService = jasmine.createSpyObj<NovelService>('NovelService', [
      'getNovel',
    ]);
    toastr = jasmine.createSpyObj<ToastrService>('ToastrService', [
      'success',
      'error',
    ]);
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>('DynamicDialogRef', [
      'close',
    ]);
    config = {
      data: {
        generatedText: 'Generated context',
        novelId: 'novel-one',
      },
    };
    novelService.getNovel.and.returnValue(of(novel()));
    compendiumService.getCompendia.and.returnValue(
      of([compendium('unlinked'), compendium('linked')]),
    );
    compendiumService.createRecord.and.returnValue(of(createdRecord()));

    TestBed.configureTestingModule({
      providers: [
        { provide: CompendiumService, useValue: compendiumService },
        { provide: NovelService, useValue: novelService },
        { provide: ToastrService, useValue: toastr },
        { provide: DynamicDialogRef, useValue: dialogRef },
        { provide: DynamicDialogConfig, useValue: config },
      ],
    });

    component = TestBed.runInInjectionContext(
      () => new GenerateCompendiumRecordResultComponent(),
    );
  });

  it('starts with generated data, form defaults, and supported record types', () => {
    expect(component.data).toEqual(config.data);
    expect(component.formGroup.getRawValue()).toEqual({
      compendiumId: '',
      type: CompendiumRecordType.Character,
      name: '',
      aliases: '',
      context: '',
    });
    expect(component.recordTypes).toEqual(Object.values(CompendiumRecordType));
    expect(component.formGroup.invalid).toBeTrue();
  });

  it('enforces text limits and supported record types', () => {
    component.formGroup.setValue({
      compendiumId: 'linked',
      type: 'unsupported' as CompendiumRecordType,
      name: 'n'.repeat(101),
      aliases: 'a'.repeat(501),
      context: '',
    });

    expect(component.formGroup.controls.type.hasError('pattern')).toBeTrue();
    expect(component.formGroup.controls.name.hasError('maxlength')).toBeTrue();
    expect(component.formGroup.controls.aliases.hasError('maxlength')).toBeTrue();
  });

  it('loads only compendia linked to the novel and selects the first one', () => {
    component.ngOnInit();

    expect(novelService.getNovel).toHaveBeenCalledOnceWith('novel-one');
    expect(compendiumService.getCompendia).toHaveBeenCalledOnceWith();
    expect(component.compendia).toEqual([compendium('linked')]);
    expect(component.formGroup.controls.compendiumId.value).toBe('linked');
    expect(component.formGroup.controls.context.value).toBe(
      'Generated context',
    );
    expect(component.isLoadingCompendia).toBeFalse();
  });

  it('keeps compendium selection required when the novel has none linked', () => {
    novelService.getNovel.and.returnValue(of(novel([])));

    component.ngOnInit();

    expect(component.compendia).toEqual([]);
    expect(component.formGroup.controls.compendiumId.value).toBe('');
    expect(component.formGroup.controls.compendiumId.invalid).toBeTrue();
  });

  it('tracks loading until both requests complete', () => {
    const novelResponse = new Subject<NovelDto>();
    const compendiaResponse = new Subject<CompendiumDto[]>();
    novelService.getNovel.and.returnValue(novelResponse);
    compendiumService.getCompendia.and.returnValue(compendiaResponse);

    component.ngOnInit();
    expect(component.isLoadingCompendia).toBeTrue();
    novelResponse.next(novel());
    novelResponse.complete();
    expect(component.isLoadingCompendia).toBeTrue();
    compendiaResponse.next([compendium('linked')]);
    compendiaResponse.complete();

    expect(component.isLoadingCompendia).toBeFalse();
  });

  it('reports novel loading failures and clears selection state', () => {
    novelService.getNovel.and.returnValue(
      throwError(() => new Error('novel failed')),
    );
    component.compendia = [compendium('old')];
    component.formGroup.controls.compendiumId.setValue('old');

    component.ngOnInit();

    expect(component.compendia).toEqual([]);
    expect(component.formGroup.controls.compendiumId.value).toBe('');
    expect(component.isLoadingCompendia).toBeFalse();
    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Failed to load the novel compendia.',
    );
  });

  it('reports compendia loading failures', () => {
    compendiumService.getCompendia.and.returnValue(
      throwError(() => new Error('compendia failed')),
    );

    component.ngOnInit();

    expect(component.compendia).toEqual([]);
    expect(component.isLoadingCompendia).toBeFalse();
    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Failed to load the novel compendia.',
    );
  });

  it('adds a trimmed unique alias and marks the field dirty', () => {
    component.formGroup.controls.aliases.setValue(' First, Second ');

    component.addAlias('Third');

    expect(component.formGroup.controls.aliases.value).toBe(
      'First, Second, Third',
    );
    expect(component.formGroup.controls.aliases.dirty).toBeTrue();
  });

  it('ignores duplicate aliases case-insensitively', () => {
    component.formGroup.controls.aliases.setValue('Hero, Friend');
    component.formGroup.controls.aliases.markAsPristine();

    component.addAlias('hErO');

    expect(component.formGroup.controls.aliases.value).toBe('Hero, Friend');
    expect(component.formGroup.controls.aliases.pristine).toBeTrue();
  });

  it('does not submit an invalid or whitespace-only name', () => {
    component.accept();
    expect(compendiumService.createRecord).not.toHaveBeenCalled();

    setValidForm();
    component.formGroup.controls.name.setValue('   ');
    component.accept();

    expect(component.formGroup.controls.name.hasError('required')).toBeTrue();
    expect(compendiumService.createRecord).not.toHaveBeenCalled();
  });

  it('does not submit while linked compendia are loading', () => {
    setValidForm();
    component.isLoadingCompendia = true;

    component.accept();

    expect(compendiumService.createRecord).not.toHaveBeenCalled();
  });

  it('maps normalized form data and closes after successful creation', () => {
    setValidForm();

    component.accept();

    expect(compendiumService.createRecord).toHaveBeenCalledOnceWith({
      name: 'Aria',
      aliases: 'Hero',
      type: CompendiumRecordType.Character,
      context: 'Generated context',
      compendiumId: 'linked',
      alwaysIncluded: false,
      characterVoiceAssignments: [],
    });
    expect(toastr.success).toHaveBeenCalledOnceWith(
      'Record Aria created successfully',
    );
    expect(dialogRef.close).toHaveBeenCalledOnceWith(true);
    expect(component.isCreating).toBeFalse();
  });

  it('uses the form context value when creating the record', () => {
    setValidForm();
    component.formGroup.controls.context.setValue('Adjusted context');

    component.accept();

    expect(compendiumService.createRecord).toHaveBeenCalledWith(
      jasmine.objectContaining({ context: 'Adjusted context' }),
    );
  });

  it('prevents duplicate creation while a request is pending', () => {
    const response = new Subject<CompendiumRecordDto>();
    compendiumService.createRecord.and.returnValue(response);
    setValidForm();

    component.accept();
    component.accept();

    expect(component.isCreating).toBeTrue();
    expect(compendiumService.createRecord).toHaveBeenCalledTimes(1);
    response.next(createdRecord());
    response.complete();
    expect(component.isCreating).toBeFalse();
  });

  it('reports creation failure and restores retry state', () => {
    compendiumService.createRecord.and.returnValue(
      throwError(() => new Error('failed')),
    );
    setValidForm();

    component.accept();

    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Failed to create record Aria',
    );
    expect(dialogRef.close).not.toHaveBeenCalled();
    expect(component.isCreating).toBeFalse();
  });

  it('closes without a result on cancel', () => {
    component.cancel();

    expect(dialogRef.close).toHaveBeenCalledOnceWith();
  });
});
