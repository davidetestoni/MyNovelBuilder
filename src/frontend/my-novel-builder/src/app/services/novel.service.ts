import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { NovelDto } from '../types/dtos/novel/novel.dto';
import { environment } from '../../environment';
import { mockObservable, mockedNovel, mockedNovels } from './mock';
import { Injectable } from '@angular/core';
import { CreateNovelDto } from '../types/dtos/novel/create-novel.dto';
import { UpdateNovelDto } from '../types/dtos/novel/update-novel.dto';

@Injectable({
  providedIn: 'root',
})
export class NovelService {
  private baseUrl = environment.api.baseUrl;
  private mocked = environment.mocked;

  constructor(private http: HttpClient) {}

  getNovels(): Observable<NovelDto[]> {
    return this.mocked
      ? mockObservable(mockedNovels)
      : this.http.get<NovelDto[]>(`${this.baseUrl}/novels`);
  }

  uploadNovelCoverImage(novelId: string, file: File): Observable<void> {
    const formData = new FormData();
    formData.append('file', file);

    return this.mocked
      ? mockObservable<void>(undefined)
      : this.http.post<void>(
          `${this.baseUrl}/novel/${novelId}/cover-image`,
          formData
        );
  }

  createNovel(novel: CreateNovelDto): Observable<NovelDto> {
    return this.mocked
      ? mockObservable(mockedNovel)
      : this.http.post<NovelDto>(`${this.baseUrl}/novel`, novel);
  }

  updateNovel(novel: UpdateNovelDto): Observable<NovelDto> {
    return this.mocked
      ? mockObservable(mockedNovel)
      : this.http.put<NovelDto>(`${this.baseUrl}/novel`, novel);
  }

  deleteNovel(novelId: string): Observable<void> {
    if (this.mocked) {
      return mockObservable<void>(undefined);
    }

    return this.http.delete<void>(`${this.baseUrl}/novel/${novelId}`);
  }
}
