import { HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { inject, Injectable } from '@angular/core';
import { NovelDto } from '../types/dtos/novel/novel.dto';
import { CreateNovelDto } from '../types/dtos/novel/create-novel.dto';
import { UpdateNovelDto } from '../types/dtos/novel/update-novel.dto';
import { Prose } from '../types/dtos/novel/prose';
import { CompendiumRecordMediaDto } from '../types/dtos/compendium-record/compendium-record-media.dto';
import { LocalStorageService } from './local-storage.service';

export type NovelExportFormat = 'markdown' | 'html' | 'pdf';

interface FloatedMedia {
  [key: string]: CompendiumRecordMediaDto[];
}

@Injectable()
export abstract class NovelService {
  private readonly localStorageService = inject(LocalStorageService);
  private readonly floatedMediaKey = 'floatedImages';

  abstract getNovels(): Observable<NovelDto[]>;
  abstract getNovel(novelId: string): Observable<NovelDto>;
  abstract getNovelProse(novelId: string): Observable<Prose>;
  abstract uploadNovelCoverImage(novelId: string, file: File): Observable<void>;
  abstract uploadProseImage(novelId: string, file: File): Observable<string>;
  abstract deleteProseImage(novelId: string, imageId: string): Observable<void>;
  abstract createNovel(novel: CreateNovelDto): Observable<NovelDto>;
  abstract updateNovel(novel: UpdateNovelDto): Observable<NovelDto>;
  abstract updateNovelProse(novelId: string, prose: Prose): Observable<void>;
  abstract replaceNovelProseFromMarkdown(
    novelId: string,
    file: File,
  ): Observable<void>;
  abstract deleteNovel(novelId: string): Observable<void>;
  abstract exportNovel(
    novelId: string,
    format: NovelExportFormat,
  ): Observable<HttpResponse<Blob>>;

  private getFloatedMedia(): FloatedMedia {
    const floatedMedia = this.localStorageService.getObjectForKey<unknown>(
      this.floatedMediaKey,
    );
    if (
      typeof floatedMedia !== 'object' ||
      floatedMedia === null ||
      Array.isArray(floatedMedia)
    ) {
      return {};
    }

    return floatedMedia as FloatedMedia;
  }

  private setFloatedMedia(floatedMedia: FloatedMedia): void {
    this.localStorageService.setObjectForKey(
      this.floatedMediaKey,
      floatedMedia,
    );
  }

  getFloatedMediaForNovel(novelId: string): CompendiumRecordMediaDto[] {
    const floatedMedia = this.getFloatedMedia();
    const media = floatedMedia[novelId];
    return Array.isArray(media) ? media : [];
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
