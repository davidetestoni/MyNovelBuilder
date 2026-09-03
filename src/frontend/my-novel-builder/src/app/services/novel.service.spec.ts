import { TestBed } from '@angular/core/testing';
import type { CompendiumRecordMediaDto } from '../types/dtos/compendium-record/compendium-record-media.dto';
import { MockNovelService } from './mock-novel.service';

describe('NovelService floated media storage', () => {
  let service: MockNovelService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [MockNovelService],
    });
    service = TestBed.inject(MockNovelService);
    localStorage.clear();
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('returns an empty list for missing or malformed storage', () => {
    expect(service.getFloatedMediaForNovel('novel-id')).toEqual([]);

    localStorage.setItem('floatedImages', '{broken');

    expect(service.getFloatedMediaForNovel('novel-id')).toEqual([]);
  });

  it('returns an empty list when a novel entry is not an array', () => {
    localStorage.setItem(
      'floatedImages',
      JSON.stringify({ 'novel-id': 'invalid' }),
    );

    expect(service.getFloatedMediaForNovel('novel-id')).toEqual([]);
  });

  it('replaces malformed storage when floated media is saved', () => {
    const media: CompendiumRecordMediaDto[] = [
      {
        id: 'media-id',
        url: '/media/image.png',
        isCurrent: true,
        isVideo: false,
      },
    ];
    localStorage.setItem('floatedImages', '{broken');

    service.setFloatedMediaForNovel('novel-id', media);

    expect(service.getFloatedMediaForNovel('novel-id')).toEqual(media);
  });
});
