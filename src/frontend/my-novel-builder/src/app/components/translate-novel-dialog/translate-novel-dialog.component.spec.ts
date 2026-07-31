import { TestBed } from '@angular/core/testing';
import { ToastrService } from 'ngx-toastr';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { of, Subject, throwError } from 'rxjs';
import {
  GenerateTextCompletion,
  GenerateTextService,
} from '../../services/generate-text.service';
import { LocalStorageService } from '../../services/local-storage.service';
import { NovelService } from '../../services/novel.service';
import {
  GenerateTextRequestDto,
  NovelTextGenerationType,
} from '../../types/dtos/generate/generate-text-request.dto';
import { NovelDto } from '../../types/dtos/novel/novel.dto';
import { Prose } from '../../types/dtos/novel/prose';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { PromptType } from '../../types/enums/prompt-type';
import { WritingLanguage } from '../../types/enums/writing-language';
import { WritingPov } from '../../types/enums/writing-pov';
import { WritingTense } from '../../types/enums/writing-tense';
import { ModelSelectComponent } from '../model-select/model-select.component';
import { PromptSelectComponent } from '../prompt-select/prompt-select.component';
import {
  TranslateNovelDialogComponent,
  TranslateNovelDialogData,
} from './translate-novel-dialog.component';

describe('TranslateNovelDialogComponent workflow', () => {
  let component: TranslateNovelDialogComponent;
  let generateTextService: jasmine.SpyObj<GenerateTextService>;
  let novelService: jasmine.SpyObj<NovelService>;
  let localStorageService: jasmine.SpyObj<LocalStorageService>;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let toastr: jasmine.SpyObj<ToastrService>;
  let config: { data: TranslateNovelDialogData };

  const sourceNovel = (): NovelDto => ({
    id: 'source-novel',
    createdAt: '2026-07-30T10:00:00Z',
    updatedAt: '2026-07-30T10:00:00Z',
    title: 'Source Novel',
    author: 'Source Author',
    brief: 'Source brief',
    coverImageUrl: null,
    tense: WritingTense.Past,
    pov: WritingPov.ThirdPersonLimited,
    language: WritingLanguage.English,
    rpgMode: true,
    mainCharacterId: 'hero',
    compendiumIds: ['world', 'characters'],
  });

  const sourceProse = (): Prose => ({
    chapters: [
      {
        title: 'Opening',
        storyEvents: [
          {
            title: 'Original event',
            date: 'Day one',
            description: 'The source event.',
          },
        ],
        sections: [
          {
            summary: 'First source summary',
            text: '<p>First source text</p>',
            images: ['image-one'],
            recordOverrides: [
              {
                compendiumRecordId: 'record-one',
                keyword: 'Old name',
                description: 'Override description',
              },
            ],
          },
          {
            summary: 'Second source summary',
            text: '<p>Second source text</p>',
            images: ['image-two'],
            recordOverrides: [],
          },
        ],
      },
      {
        title: 'Ending',
        storyEvents: [],
        sections: [
          {
            summary: 'Final source summary',
            text: '<p>Final source text</p>',
            images: [],
            recordOverrides: [],
          },
        ],
      },
    ],
  });

  const createdNovel = (): NovelDto => ({
    ...sourceNovel(),
    id: 'translated-novel',
    title: 'Source Novel (italian)',
    language: WritingLanguage.Italian,
    coverImageUrl: null,
    compendiumIds: [],
  });

  const completion = (
    content: string,
    parseError: string | null = null,
    rawResponse = content,
  ): GenerateTextCompletion => ({ content, parseError, rawResponse });

  const translatedChapter = (chapterIndex: number): string =>
    chapterIndex === 0
      ? JSON.stringify({
          chapterTitle: '  Apertura  ',
          storyEvents: [
            {
              title: '  Evento tradotto  ',
              date: '  Primo giorno  ',
              description: '  La descrizione.  ',
            },
          ],
          sections: [
            {
              sectionIndex: 1,
              summary: '  Secondo riassunto  ',
              text: '  <p>Secondo testo</p>  ',
            },
            {
              sectionIndex: 0,
              summary: '  Primo riassunto  ',
              text: '  <p>Primo testo</p>  ',
            },
          ],
        })
      : JSON.stringify({
          chapterTitle: '  Finale  ',
          storyEvents: [],
          sections: [
            {
              sectionIndex: 0,
              summary: '  Riassunto finale  ',
              text: '  <p>Testo finale</p>  ',
            },
          ],
        });

  const setValidForm = (): void => {
    component.formGroup.setValue({
      title: '',
      targetLanguage: WritingLanguage.Italian,
      promptId: '  translate-prompt  ',
      model: '  structured-model  ',
      instructions: '  Preserve character names.  ',
    });
  };

  const setAcceptableTranslation = (): Prose => {
    const translated: Prose = {
      chapters: [
        {
          title: 'Apertura',
          storyEvents: [],
          sections: [
            {
              summary: 'Riassunto',
              text: '<p>Testo</p>',
              images: ['image-one'],
              recordOverrides: [],
            },
          ],
        },
      ],
    };
    component.translatedProse = translated;
    (
      component as unknown as {
        translatedLanguage: WritingLanguage | null;
      }
    ).translatedLanguage = WritingLanguage.Italian;
    return translated;
  };

  beforeEach(() => {
    generateTextService = jasmine.createSpyObj<GenerateTextService>(
      'GenerateTextService',
      ['generateTextCompletion'],
    );
    novelService = jasmine.createSpyObj<NovelService>('NovelService', [
      'createNovel',
      'updateNovel',
      'uploadNovelCoverImage',
      'updateNovelProse',
    ]);
    localStorageService = jasmine.createSpyObj<LocalStorageService>(
      'LocalStorageService',
      ['setNestedStringForKey'],
    );
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>('DynamicDialogRef', [
      'close',
    ]);
    toastr = jasmine.createSpyObj<ToastrService>('ToastrService', [
      'success',
      'error',
    ]);
    config = {
      data: {
        novel: sourceNovel(),
        prose: sourceProse(),
        prompts: [],
      },
    };

    generateTextService.generateTextCompletion.and.callFake((request) =>
      of(
        completion(
          translatedChapter(
            (
              request.contextInfo as unknown as {
                chapterIndex: number;
              }
            ).chapterIndex,
          ),
        ),
      ),
    );
    novelService.createNovel.and.returnValue(of(createdNovel()));
    novelService.updateNovel.and.returnValue(of(createdNovel()));
    novelService.uploadNovelCoverImage.and.returnValue(of(undefined));
    novelService.updateNovelProse.and.returnValue(of(undefined));

    TestBed.configureTestingModule({
      providers: [
        { provide: GenerateTextService, useValue: generateTextService },
        { provide: NovelService, useValue: novelService },
        { provide: LocalStorageService, useValue: localStorageService },
        { provide: DynamicDialogConfig, useValue: config },
        { provide: DynamicDialogRef, useValue: dialogRef },
        { provide: ToastrService, useValue: toastr },
      ],
    });

    component = TestBed.runInInjectionContext(
      () => new TranslateNovelDialogComponent(),
    );
  });

  it('starts with a language different from the source and required generation fields', () => {
    expect(component.data).toBe(config.data);
    expect(component.formGroup.getRawValue()).toEqual({
      title: '',
      targetLanguage: WritingLanguage.Italian,
      promptId: '',
      model: '',
      instructions: '',
    });
    expect(component.writingLanguages).toEqual(Object.values(WritingLanguage));
    expect(component.promptType).toBe(PromptType.TranslateNovel);
    expect(component.canGenerate).toBeFalse();
  });

  it('enforces title and instruction length limits', async () => {
    component.formGroup.patchValue({
      title: 't'.repeat(101),
      promptId: 'prompt',
      model: 'model',
      instructions: 'i'.repeat(5_001),
    });

    expect(component.formGroup.controls.title.hasError('maxlength')).toBeTrue();
    expect(
      component.formGroup.controls.instructions.hasError('maxlength'),
    ).toBeTrue();
    expect(component.canGenerate).toBeFalse();

    await component.generate();
    expect(generateTextService.generateTextCompletion).not.toHaveBeenCalled();
  });

  it('tracks whether matching translation prompts are available', () => {
    expect(component.hasTranslationPromptOptions).toBeFalse();

    component.onPromptOptionsChanged(2);

    expect(component.promptCount).toBe(2);
    expect(component.hasTranslationPromptOptions).toBeTrue();
  });

  it('can use valid prompt and model values exposed by child selectors', () => {
    component.formGroup.patchValue({
      targetLanguage: WritingLanguage.Italian,
      promptId: '',
      model: '',
    });
    component.promptSelect = {
      value: '  child-prompt  ',
    } as unknown as PromptSelectComponent;
    component.modelSelect = {
      value: '  child-model  ',
    } as unknown as ModelSelectComponent;

    expect(component.canGenerate).toBeTrue();
  });

  it('does not generate with missing or whitespace-only selections', async () => {
    await component.generate();
    setValidForm();
    component.formGroup.controls.promptId.setValue('   ');
    component.promptSelect = {
      value: '   ',
    } as unknown as PromptSelectComponent;

    await component.generate();

    expect(generateTextService.generateTextCompletion).not.toHaveBeenCalled();
    expect(
      localStorageService.setNestedStringForKey,
    ).not.toHaveBeenCalled();
  });

  it('rejects the source language before starting translation', async () => {
    setValidForm();
    component.formGroup.controls.targetLanguage.setValue(
      WritingLanguage.English,
    );

    await component.generate();

    expect(component.generationError).toBe(
      'Please choose a target language different from the source novel language.',
    );
    expect(generateTextService.generateTextCompletion).not.toHaveBeenCalled();
  });

  it('normalizes selections and sends one contextual request per chapter', async () => {
    setValidForm();

    await component.generate();

    expect(
      localStorageService.setNestedStringForKey,
    ).toHaveBeenCalledOnceWith(
      LocalStorageKey.RecentPrompts,
      PromptType.TranslateNovel,
      'translate-prompt',
    );
    expect(generateTextService.generateTextCompletion).toHaveBeenCalledTimes(2);
    expect(
      generateTextService.generateTextCompletion.calls.argsFor(0)[0],
    ).toEqual({
      model: 'structured-model',
      promptId: 'translate-prompt',
      contextInfo: {
        $type: NovelTextGenerationType.TranslateNovel,
        novelId: 'source-novel',
        chapterIndex: 0,
        targetLanguage: WritingLanguage.Italian,
        instructions: 'Preserve character names.',
      },
    } as GenerateTextRequestDto);
    expect(
      generateTextService.generateTextCompletion.calls.argsFor(1)[0]
        .contextInfo,
    ).toEqual(
      jasmine.objectContaining({
        chapterIndex: 1,
        targetLanguage: WritingLanguage.Italian,
      }),
    );
  });

  it('normalizes blank optional instructions to null', async () => {
    setValidForm();
    component.formGroup.controls.instructions.setValue('   ');

    await component.generate();

    expect(
      generateTextService.generateTextCompletion.calls.first().args[0]
        .contextInfo,
    ).toEqual(jasmine.objectContaining({ instructions: null }));
  });

  it('assembles translated prose in source order and preserves section assets', async () => {
    setValidForm();

    await component.generate();

    expect(component.translatedProse).toEqual({
      chapters: [
        {
          title: 'Apertura',
          storyEvents: [
            {
              title: 'Evento tradotto',
              date: 'Primo giorno',
              description: 'La descrizione.',
            },
          ],
          sections: [
            {
              summary: 'Primo riassunto',
              text: '<p>Primo testo</p>',
              images: ['image-one'],
              recordOverrides: [
                {
                  compendiumRecordId: 'record-one',
                  keyword: 'Old name',
                  description: 'Override description',
                },
              ],
            },
            {
              summary: 'Secondo riassunto',
              text: '<p>Secondo testo</p>',
              images: ['image-two'],
              recordOverrides: [],
            },
          ],
        },
        {
          title: 'Finale',
          storyEvents: [],
          sections: [
            {
              summary: 'Riassunto finale',
              text: '<p>Testo finale</p>',
              images: [],
              recordOverrides: [],
            },
          ],
        },
      ],
    });
    expect(component.progressItems).toEqual([
      {
        chapterIndex: 0,
        chapterTitle: 'Apertura',
        isCompleted: true,
      },
      {
        chapterIndex: 1,
        chapterTitle: 'Finale',
        isCompleted: true,
      },
    ]);
    expect(component.canAccept).toBeTrue();
  });

  it('falls back to the source chapter title when the translation is blank', async () => {
    generateTextService.generateTextCompletion.and.callFake((request) => {
      const chapterIndex = (
        request.contextInfo as unknown as { chapterIndex: number }
      ).chapterIndex;
      const parsed = JSON.parse(translatedChapter(chapterIndex));
      parsed.chapterTitle = '   ';
      return of(completion(JSON.stringify(parsed)));
    });
    setValidForm();

    await component.generate();

    expect(component.translatedProse?.chapters.map((chapter) => chapter.title))
      .toEqual(['Opening', 'Ending']);
  });

  it('translates chapters sequentially and updates progress as each completes', async () => {
    const firstResponse = new Subject<GenerateTextCompletion>();
    const secondResponse = new Subject<GenerateTextCompletion>();
    generateTextService.generateTextCompletion.and.returnValues(
      firstResponse,
      secondResponse,
    );
    setValidForm();

    const generation = component.generate();
    expect(generateTextService.generateTextCompletion).toHaveBeenCalledTimes(1);
    expect(component.progressItems.every((item) => !item.isCompleted)).toBeTrue();

    firstResponse.next(completion(translatedChapter(0)));
    firstResponse.complete();
    await Promise.resolve();
    await Promise.resolve();

    expect(generateTextService.generateTextCompletion).toHaveBeenCalledTimes(2);
    expect(component.progressItems[0].isCompleted).toBeTrue();
    expect(component.progressItems[1].isCompleted).toBeFalse();

    secondResponse.next(completion(translatedChapter(1)));
    secondResponse.complete();
    await generation;
    expect(component.progressItems[1].isCompleted).toBeTrue();
  });

  it('reports streamed-response decoder errors', async () => {
    generateTextService.generateTextCompletion.and.returnValue(
      of(completion('', 'invalid NDJSON', 'raw chunks')),
    );
    setValidForm();

    await component.generate();

    expect(component.generationError).toBe(
      'Unable to read the streamed response: invalid NDJSON',
    );
    expect(component.translatedProse).toBeNull();
    expect(component.isGenerating).toBeFalse();
  });

  it('rejects malformed JSON and invalid top-level shapes', async () => {
    setValidForm();
    for (const output of [
      '{not-json',
      JSON.stringify([]),
      JSON.stringify({
        chapterTitle: 'Title',
        storyEvents: {},
        sections: [],
      }),
    ]) {
      generateTextService.generateTextCompletion.and.returnValue(
        of(completion(output)),
      );

      await component.generate();

      expect(component.generationError).toBe(
        'Unable to parse translated output for chapter 1.',
      );
      expect(component.translatedProse).toBeNull();
    }
  });

  it('rejects structurally invalid story events', async () => {
    setValidForm();
    const invalidOutputs = [
      null,
      { title: 'Missing fields' },
      { title: 'Title', date: 1, description: 'Description' },
    ];

    for (const event of invalidOutputs) {
      generateTextService.generateTextCompletion.and.returnValue(
        of(
          completion(
            JSON.stringify({
              chapterTitle: 'Title',
              storyEvents: [event],
              sections: [],
            }),
          ),
        ),
      );

      await component.generate();
      expect(component.translatedProse).toBeNull();
      expect(component.generationError).toContain('chapter 1');
    }
  });

  it('rejects duplicate, non-integer, and negative section indexes', async () => {
    setValidForm();
    for (const sections of [
      [
        { sectionIndex: 0, summary: 'One', text: 'One' },
        { sectionIndex: 0, summary: 'Duplicate', text: 'Duplicate' },
      ],
      [{ sectionIndex: 0.5, summary: 'Fraction', text: 'Fraction' }],
      [{ sectionIndex: -1, summary: 'Negative', text: 'Negative' }],
    ]) {
      generateTextService.generateTextCompletion.and.returnValue(
        of(
          completion(
            JSON.stringify({
              chapterTitle: 'Title',
              storyEvents: [],
              sections,
            }),
          ),
        ),
      );

      await component.generate();
      expect(component.generationError).toBe(
        'Unable to parse translated output for chapter 1.',
      );
    }
  });

  it('rejects section indexes outside the source chapter', async () => {
    generateTextService.generateTextCompletion.and.returnValue(
      of(
        completion(
          JSON.stringify({
            chapterTitle: 'Title',
            storyEvents: [],
            sections: [
              { sectionIndex: 0, summary: 'One', text: 'One' },
              { sectionIndex: 2, summary: 'Outside', text: 'Outside' },
            ],
          }),
        ),
      ),
    );
    setValidForm();

    await component.generate();

    expect(component.generationError).toBe(
      'The generated output referenced invalid section index 2 for chapter 1.',
    );
  });

  it('rejects output that omits a source section', async () => {
    generateTextService.generateTextCompletion.and.returnValue(
      of(
        completion(
          JSON.stringify({
            chapterTitle: 'Title',
            storyEvents: [],
            sections: [
              { sectionIndex: 0, summary: 'Only one', text: 'Only one' },
            ],
          }),
        ),
      ),
    );
    setValidForm();

    await component.generate();

    expect(component.generationError).toBe(
      'The generated output did not include every section in chapter 1.',
    );
  });

  it('stops after a provider failure and exposes Error details', async () => {
    generateTextService.generateTextCompletion.and.returnValue(
      throwError(() => new Error('provider offline')),
    );
    setValidForm();

    await component.generate();

    expect(component.generationError).toBe('provider offline');
    expect(generateTextService.generateTextCompletion).toHaveBeenCalledTimes(1);
    expect(component.translatedProse).toBeNull();
    expect(component.isGenerating).toBeFalse();
  });

  it('uses a stable fallback for non-Error failures', async () => {
    generateTextService.generateTextCompletion.and.returnValue(
      throwError(() => 'offline'),
    );
    setValidForm();

    await component.generate();

    expect(component.generationError).toBe('Failed to translate the novel.');
  });

  it('prevents duplicate generation while a chapter request is pending', async () => {
    const response = new Subject<GenerateTextCompletion>();
    generateTextService.generateTextCompletion.and.returnValue(response);
    setValidForm();

    const generation = component.generate();
    await component.generate();

    expect(generateTextService.generateTextCompletion).toHaveBeenCalledTimes(1);
    expect(component.isGenerating).toBeTrue();
    response.error(new Error('finish pending request'));
    await generation;
  });

  it('ignores a late generation completion after destruction', async () => {
    const response = new Subject<GenerateTextCompletion>();
    generateTextService.generateTextCompletion.and.returnValue(response);
    setValidForm();

    const generation = component.generate();
    component.ngOnDestroy();
    response.next(completion(translatedChapter(0)));
    response.complete();
    await generation;

    expect(component.translatedProse).toBeNull();
    expect(component.generationError).toBeNull();
    expect(component.isGenerating).toBeFalse();
  });

  it('supports a source novel with no chapters without making requests', async () => {
    config.data.prose = { chapters: [] };
    component = TestBed.runInInjectionContext(
      () => new TranslateNovelDialogComponent(),
    );
    setValidForm();

    await component.generate();

    expect(generateTextService.generateTextCompletion).not.toHaveBeenCalled();
    expect(component.translatedProse).toEqual({ chapters: [] });
    expect(component.progressItems).toEqual([]);
    expect(component.canAccept).toBeTrue();
  });

  it('invalidates generated prose acceptance when the target language changes', async () => {
    setValidForm();
    await component.generate();
    expect(component.canAccept).toBeTrue();

    component.formGroup.controls.targetLanguage.setValue(
      WritingLanguage.French,
    );

    expect(component.canAccept).toBeFalse();
    await component.accept();
    expect(novelService.createNovel).not.toHaveBeenCalled();
  });

  it('does not accept before a valid translation exists', async () => {
    setValidForm();

    await component.accept();

    expect(novelService.createNovel).not.toHaveBeenCalled();
    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('creates, configures, and populates the translated novel', async () => {
    setValidForm();
    const translated = setAcceptableTranslation();

    await component.accept();

    expect(novelService.createNovel).toHaveBeenCalledOnceWith({
      title: 'Source Novel (italian)',
      author: 'Source Author',
      brief: 'Source brief',
      tense: WritingTense.Past,
      pov: WritingPov.ThirdPersonLimited,
      language: WritingLanguage.Italian,
      rpgMode: true,
      mainCharacterId: 'hero',
    });
    expect(novelService.updateNovel).toHaveBeenCalledOnceWith({
      id: 'translated-novel',
      title: 'Source Novel (italian)',
      author: 'Source Author',
      brief: 'Source brief',
      tense: WritingTense.Past,
      pov: WritingPov.ThirdPersonLimited,
      language: WritingLanguage.Italian,
      rpgMode: true,
      mainCharacterId: 'hero',
      compendiumIds: ['world', 'characters'],
    });
    expect(novelService.updateNovelProse).toHaveBeenCalledOnceWith(
      'translated-novel',
      translated,
    );
    expect(toastr.success).toHaveBeenCalledOnceWith(
      'Translated novel created.',
    );
    expect(dialogRef.close).toHaveBeenCalledOnceWith({
      novelId: 'translated-novel',
    });
    expect(component.isSaving).toBeFalse();
  });

  it('uses a normalized custom title', async () => {
    setValidForm();
    setAcceptableTranslation();
    component.formGroup.controls.title.setValue('  Il Romanzo  ');

    await component.accept();

    expect(novelService.createNovel).toHaveBeenCalledOnceWith(
      jasmine.objectContaining({ title: 'Il Romanzo' }),
    );
  });

  it('copies an available source cover to the translated novel', async () => {
    config.data.novel.coverImageUrl = '/covers/source.png';
    const coverBlob = new Blob(['cover'], { type: 'image/webp' });
    const fetchSpy = spyOn(window, 'fetch').and.resolveTo(
      new Response(coverBlob, { status: 200 }),
    );
    setValidForm();
    setAcceptableTranslation();

    await component.accept();

    expect(fetchSpy).toHaveBeenCalledOnceWith('/covers/source.png');
    expect(novelService.uploadNovelCoverImage).toHaveBeenCalledTimes(1);
    const [novelId, coverFile] =
      novelService.uploadNovelCoverImage.calls.first().args;
    expect(novelId).toBe('translated-novel');
    expect(coverFile.name).toBe('cover.png');
    expect(coverFile.type).toBe('image/webp');
  });

  it('skips a cover when its response is unsuccessful', async () => {
    config.data.novel.coverImageUrl = '/covers/missing.png';
    spyOn(window, 'fetch').and.resolveTo(
      new Response(null, { status: 404 }),
    );
    setValidForm();
    setAcceptableTranslation();

    await component.accept();

    expect(novelService.uploadNovelCoverImage).not.toHaveBeenCalled();
    expect(novelService.updateNovelProse).toHaveBeenCalled();
    expect(dialogRef.close).toHaveBeenCalled();
  });

  it('treats cover fetch and upload failures as best-effort', async () => {
    config.data.novel.coverImageUrl = '/covers/source.png';
    spyOn(window, 'fetch').and.resolveTo(
      new Response(new Blob(['cover']), { status: 200 }),
    );
    novelService.uploadNovelCoverImage.and.returnValue(
      throwError(() => new Error('cover upload failed')),
    );
    setValidForm();
    setAcceptableTranslation();

    await component.accept();

    expect(novelService.updateNovelProse).toHaveBeenCalled();
    expect(toastr.error).not.toHaveBeenCalled();
    expect(toastr.success).toHaveBeenCalled();
    expect(dialogRef.close).toHaveBeenCalled();
  });

  it('continues translated-novel creation when the cover cannot be fetched', async () => {
    config.data.novel.coverImageUrl = '/covers/unavailable.png';
    spyOn(window, 'fetch').and.returnValue(
      Promise.reject(new Error('cover fetch failed')),
    );
    setValidForm();
    setAcceptableTranslation();

    await component.accept();

    expect(novelService.uploadNovelCoverImage).not.toHaveBeenCalled();
    expect(novelService.updateNovelProse).toHaveBeenCalled();
    expect(toastr.error).not.toHaveBeenCalled();
    expect(toastr.success).toHaveBeenCalled();
    expect(dialogRef.close).toHaveBeenCalled();
  });

  it('prevents duplicate saving while novel creation is pending', async () => {
    const createResponse = new Subject<NovelDto>();
    novelService.createNovel.and.returnValue(createResponse);
    setValidForm();
    setAcceptableTranslation();

    const saving = component.accept();
    await component.accept();

    expect(novelService.createNovel).toHaveBeenCalledTimes(1);
    expect(component.isSaving).toBeTrue();
    createResponse.next(createdNovel());
    createResponse.complete();
    await saving;
    expect(component.isSaving).toBeFalse();
  });

  it('reports creation failures without closing the dialog', async () => {
    novelService.createNovel.and.returnValue(
      throwError(() => new Error('creation failed')),
    );
    setValidForm();
    setAcceptableTranslation();

    await component.accept();

    expect(toastr.error).toHaveBeenCalledOnceWith('creation failed');
    expect(novelService.updateNovel).not.toHaveBeenCalled();
    expect(dialogRef.close).not.toHaveBeenCalled();
    expect(component.isSaving).toBeFalse();
  });

  it('stops and reports metadata update failures', async () => {
    novelService.updateNovel.and.returnValue(
      throwError(() => new Error('metadata failed')),
    );
    setValidForm();
    setAcceptableTranslation();

    await component.accept();

    expect(toastr.error).toHaveBeenCalledOnceWith('metadata failed');
    expect(novelService.updateNovelProse).not.toHaveBeenCalled();
    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('reports prose persistence failures without reporting success', async () => {
    novelService.updateNovelProse.and.returnValue(
      throwError(() => new Error('prose failed')),
    );
    setValidForm();
    setAcceptableTranslation();

    await component.accept();

    expect(toastr.error).toHaveBeenCalledOnceWith('prose failed');
    expect(toastr.success).not.toHaveBeenCalled();
    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('uses a stable save error for non-Error failures', async () => {
    novelService.createNovel.and.returnValue(throwError(() => 'failed'));
    setValidForm();
    setAcceptableTranslation();

    await component.accept();

    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Failed to create translated novel.',
    );
  });
});
