import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { NovelDto } from '../types/dtos/novel/novel.dto';
import { environment } from '../../environment';
import { mockObservable, mockedNovels } from './mock';
import { Injectable } from '@angular/core';

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

  getNovelCoverImageUrl(novelId: string): string {
    return this.mocked
      ? `https://picsum.photos/seed/${novelId}/200/300`
      : `${this.baseUrl}/novels/${novelId}/cover`;
  }
}
