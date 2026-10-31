import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { AudiobookJob, AudiobookPreview, AudiobookSettings } from '../types/dtos/generate/audiobook.dto';

/** Saved-novel audiobook operations. Download URLs are handed directly to the browser. */
@Injectable()
export abstract class AudiobookService {
  /** True only for the demo adapter, whose downloads contain sample silence. */
  readonly isDemo: boolean = false;
  /** Captures saved inputs and estimates reuse without paid generation. */
  abstract preview(novelId: string, settings: AudiobookSettings): Observable<AudiobookPreview>;
  /** Starts the exact preview; reuse the key when repeating a submission after a lost response. */
  abstract start(novelId: string, snapshotId: string, idempotencyKey: string, maxParallelSections?: number): Observable<AudiobookJob>;
  /** Retrieves durable progress so a reopened dialog can reconnect. */
  abstract list(novelId: string): Observable<AudiobookJob[]>;
  /** Requests cooperative cancellation while retaining completed audio. */
  abstract cancel(novelId: string, jobId: string): Observable<AudiobookJob>;
  /** Retries original inputs, including rebuilding a missing completed archive. */
  abstract retry(novelId: string, jobId: string, maxParallelSections?: number): Observable<AudiobookJob>;
  /** Returns a browser download URL, avoiding a large JavaScript Blob. */
  abstract downloadUrl(novelId: string, jobId: string): string;
}
