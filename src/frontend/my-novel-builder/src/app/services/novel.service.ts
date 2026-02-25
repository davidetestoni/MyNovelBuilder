import { HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Injectable } from '@angular/core';
import { NovelDto } from '../types/dtos/novel/novel.dto';
import { CreateNovelDto } from '../types/dtos/novel/create-novel.dto';
import { UpdateNovelDto } from '../types/dtos/novel/update-novel.dto';
import { Prose } from '../types/dtos/novel/prose';
import { CompendiumRecordMediaDto } from '../types/dtos/compendium-record/compendium-record-media.dto';

interface FloatedMedia {
  [key: string]: CompendiumRecordMediaDto[];
}

@Injectable()
export abstract class NovelService {
  private floatedMediaKey = 'floatedImages';

  abstract getNovels(): Observable<NovelDto[]>;
  abstract getNovel(novelId: string): Observable<NovelDto>;
  abstract getNovelProse(novelId: string): Observable<Prose>;
  abstract uploadNovelCoverImage(novelId: string, file: File): Observable<void>;
  abstract uploadProseImage(novelId: string, file: File): Observable<string>;
  abstract deleteProseImage(novelId: string, imageId: string): Observable<void>;
  abstract createNovel(novel: CreateNovelDto): Observable<NovelDto>;
  abstract updateNovel(novel: UpdateNovelDto): Observable<NovelDto>;
  abstract updateNovelProse(novelId: string, prose: Prose): Observable<void>;
  abstract deleteNovel(novelId: string): Observable<void>;
  abstract exportNovelToMarkdown(novelId: string): Observable<HttpResponse<Blob>>;

  private getFloatedMedia(): FloatedMedia {
    const floatedMedia = localStorage.getItem(this.floatedMediaKey);
    return floatedMedia ? JSON.parse(floatedMedia) : {};
  }

  private setFloatedMedia(floatedMedia: FloatedMedia): void {
    localStorage.setItem(this.floatedMediaKey, JSON.stringify(floatedMedia));
  }

  getFloatedMediaForNovel(novelId: string): CompendiumRecordMediaDto[] {
    const floatedMedia = this.getFloatedMedia();
    return floatedMedia[novelId] || [];
  }

  setFloatedMediaForNovel(
    novelId: string,
    media: CompendiumRecordMediaDto[],
  ): void {
    const floatedMedia = this.getFloatedMedia();
    floatedMedia[novelId] = media;
    this.setFloatedMedia(floatedMedia);
  }
}
