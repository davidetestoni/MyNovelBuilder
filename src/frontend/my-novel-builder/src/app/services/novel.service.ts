import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { NovelDto } from '../types/dtos/novel/novel.dto';
import { environment } from '../../environment';
import { mockObservable, mockedNovels, mockedProse } from './mock';
import { Injectable } from '@angular/core';
import { CreateNovelDto } from '../types/dtos/novel/create-novel.dto';
import { UpdateNovelDto } from '../types/dtos/novel/update-novel.dto';
import { Prose } from '../types/dtos/novel/prose';

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

  getNovel(novelId: string): Observable<NovelDto> {
    return this.mocked
      ? mockObservable(mockedNovels[0])
      : this.http.get<NovelDto>(`${this.baseUrl}/novel/${novelId}`);
  }

  getNovelProse(novelId: string): Observable<Prose> {
    return this.mocked
      ? mockObservable(mockedProse)
      : this.http.get<Prose>(`${this.baseUrl}/novel/${novelId}/prose`);
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
      ? mockObservable(mockedNovels[0])
      : this.http.post<NovelDto>(`${this.baseUrl}/novel`, novel);
  }

  updateNovel(novel: UpdateNovelDto): Observable<NovelDto> {
    return this.mocked
      ? mockObservable(mockedNovels[0])
      : this.http.put<NovelDto>(`${this.baseUrl}/novel`, novel);
  }

  updateNovelProse(novelId: string, prose: Prose): Observable<void> {
    return this.mocked
      ? mockObservable<void>(undefined)
      : this.http.put<void>(`${this.baseUrl}/novel/${novelId}/prose`, prose);
  }

  deleteNovel(novelId: string): Observable<void> {
    if (this.mocked) {
      return mockObservable<void>(undefined);
    }

    return this.http.delete<void>(`${this.baseUrl}/novel/${novelId}`);
  }
}
