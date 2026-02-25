import { HttpResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateNovelDto } from '../types/dtos/novel/create-novel.dto';
import { NovelDto } from '../types/dtos/novel/novel.dto';
import { Prose } from '../types/dtos/novel/prose';
import { UpdateNovelDto } from '../types/dtos/novel/update-novel.dto';
import { mockedNovels, mockedProse } from './mocks/mock-novel.data';
import { mockObservable } from './mocks/mock-utils';
import { NovelService } from './novel.service';

@Injectable()
export class MockNovelService extends NovelService {
  getNovels(): Observable<NovelDto[]> {
    return mockObservable(mockedNovels);
  }

  getNovel(_novelId: string): Observable<NovelDto> {
    return mockObservable(mockedNovels[0]);
  }

  getNovelProse(_novelId: string): Observable<Prose> {
    return mockObservable(mockedProse);
  }

  uploadNovelCoverImage(_novelId: string, _file: File): Observable<void> {
    return mockObservable<void>(undefined);
  }

  uploadProseImage(_novelId: string, _file: File): Observable<string> {
    return mockObservable('https://picsum.photos/200/300');
  }

  deleteProseImage(_novelId: string, _imageId: string): Observable<void> {
    return mockObservable<void>(undefined);
  }

  createNovel(_novel: CreateNovelDto): Observable<NovelDto> {
    return mockObservable(mockedNovels[0]);
  }

  updateNovel(_novel: UpdateNovelDto): Observable<NovelDto> {
    return mockObservable(mockedNovels[0]);
  }

  updateNovelProse(_novelId: string, _prose: Prose): Observable<void> {
    return mockObservable<void>(undefined);
  }

  deleteNovel(_novelId: string): Observable<void> {
    return mockObservable<void>(undefined);
  }

  exportNovelToMarkdown(_novelId: string): Observable<HttpResponse<Blob>> {
    return mockObservable(
      new HttpResponse({
        body: new Blob(['# Mock Novel Export\n\nThis is a mocked markdown export.'], {
          type: 'text/markdown',
        }),
        status: 200,
      }),
    );
  }
}
