import { TestBed } from '@angular/core/testing';
import { NovelTextGenerationType } from '../types/dtos/generate/generate-text-request.dto';
import { MockGenerateTextService } from './mock-generate-text.service';

describe('GenerateTextService local storage', () => {
  let service: MockGenerateTextService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [MockGenerateTextService],
    });
    service = TestBed.inject(MockGenerateTextService);
    localStorage.clear();
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('returns an empty recent-model list for malformed storage', () => {
    localStorage.setItem('recentlyUsedModels', '{broken');

    expect(service.getRecentlyUsedModels()).toEqual([]);
  });

  it('ignores non-string recent-model values', () => {
    localStorage.setItem(
      'recentlyUsedModels',
      JSON.stringify(['model-a', null, 42, 'model-b']),
    );

    expect(service.getRecentlyUsedModels()).toEqual(['model-a', 'model-b']);
  });

  it('replaces malformed storage when a model is used', () => {
    localStorage.setItem('recentlyUsedModels', '{broken');

    service.generateText({
      model: 'model-a',
      promptId: 'prompt-id',
      contextInfo: {
        $type: NovelTextGenerationType.GenerateText,
        novelId: 'novel-id',
      },
    });

    expect(service.getRecentlyUsedModels()).toEqual(['model-a']);
  });
});
