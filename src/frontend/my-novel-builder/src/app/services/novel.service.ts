import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { NovelDto } from '../types/dtos/novel/novel.dto';
import { environment } from '../../environment';
import { mockObservable, mockedNovels } from './mock';
import { Injectable } from '@angular/core';
import { NovelCoverImageDto } from '../types/dtos/novel/novel-cover-image.dto';

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

  getNovelCoverImageUrl(novelId: string): Observable<string | null> {
    return this.mocked
      ? mockObservable(`https://picsum.photos/seed/${novelId}/200/300`)
      : this.http
          .get<NovelCoverImageDto>(
            `${this.baseUrl}/novel/${novelId}/cover-image`
          )
          .pipe(map((coverImage) => coverImage?.location ?? null));
  }
}
