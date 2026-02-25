import { HttpClient, HttpResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environment';
import { CreateNovelDto } from '../types/dtos/novel/create-novel.dto';
import { NovelDto } from '../types/dtos/novel/novel.dto';
import { Prose } from '../types/dtos/novel/prose';
import { UpdateNovelDto } from '../types/dtos/novel/update-novel.dto';
import { NovelService } from './novel.service';

@Injectable()
export class ApiNovelService extends NovelService {
  private http = inject(HttpClient);

  private baseUrl = environment.api.baseUrl;

  getNovels(): Observable<NovelDto[]> {
    return this.http.get<NovelDto[]>(`${this.baseUrl}/novels`);
  }

  getNovel(novelId: string): Observable<NovelDto> {
    return this.http.get<NovelDto>(`${this.baseUrl}/novel/${novelId}`);
  }

  getNovelProse(novelId: string): Observable<Prose> {
    return this.http.get<Prose>(`${this.baseUrl}/novel/${novelId}/prose`);
  }

  uploadNovelCoverImage(novelId: string, file: File): Observable<void> {
    const formData = new FormData();
    formData.append('file', file);

    return this.http.post<void>(`${this.baseUrl}/novel/${novelId}/cover-image`, formData);
  }

  uploadProseImage(novelId: string, file: File): Observable<string> {
    const formData = new FormData();
    formData.append('file', file);

    return this.http.post<string>(`${this.baseUrl}/novel/${novelId}/prose-image`, formData);
  }

  deleteProseImage(novelId: string, imageId: string): Observable<void> {
    const encodedImageId = encodeURIComponent(imageId);
    return this.http.delete<void>(`${this.baseUrl}/novel/${novelId}/prose-image/${encodedImageId}`);
  }

  createNovel(novel: CreateNovelDto): Observable<NovelDto> {
    return this.http.post<NovelDto>(`${this.baseUrl}/novel`, novel);
  }

  updateNovel(novel: UpdateNovelDto): Observable<NovelDto> {
    return this.http.put<NovelDto>(`${this.baseUrl}/novel`, novel);
  }

  updateNovelProse(novelId: string, prose: Prose): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/novel/${novelId}/prose`, prose);
  }

  deleteNovel(novelId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/novel/${novelId}`);
  }

  exportNovelToMarkdown(novelId: string): Observable<HttpResponse<Blob>> {
    return this.http.get(`${this.baseUrl}/novel/${novelId}/export/markdown`, {
      responseType: 'blob' as const,
      observe: 'response' as const,
    });
  }
}
