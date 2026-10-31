import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environment';
import { AudiobookService } from './audiobook.service';
import { AudiobookJob, AudiobookPreview, AudiobookSettings } from '../types/dtos/generate/audiobook.dto';

/** HTTP adapter for the filesystem-backed audiobook worker. */
@Injectable()
export class ApiAudiobookService extends AudiobookService {
  private readonly http = inject(HttpClient);
  private root(novelId: string): string { return `${environment.api.baseUrl}/novel/${encodeURIComponent(novelId)}/audiobook`; }
  /** @inheritdoc */
  preview(novelId: string, settings: AudiobookSettings) {
    return this.http.post<AudiobookPreview>(`${this.root(novelId)}/preview`, settings);
  }
  /** @inheritdoc */
  start(novelId: string, snapshotId: string, idempotencyKey: string, maxParallelSections = 1) {
    return this.http.post<AudiobookJob>(`${this.root(novelId)}/jobs`, { snapshotId, idempotencyKey, maxParallelSections });
  }
  /** @inheritdoc */
  list(novelId: string) { return this.http.get<AudiobookJob[]>(`${this.root(novelId)}/jobs`); }
  /** @inheritdoc */
  cancel(novelId: string, jobId: string) { return this.http.post<AudiobookJob>(`${this.root(novelId)}/jobs/${encodeURIComponent(jobId)}/cancel`, {}); }
  /** @inheritdoc */
  retry(novelId: string, jobId: string, maxParallelSections?: number) { return this.http.post<AudiobookJob>(`${this.root(novelId)}/jobs/${encodeURIComponent(jobId)}/retry`, maxParallelSections === undefined ? {} : { maxParallelSections }); }
  /** @inheritdoc */
  downloadUrl(novelId: string, jobId: string): string { return `${this.root(novelId)}/jobs/${encodeURIComponent(jobId)}/download`; }
}
