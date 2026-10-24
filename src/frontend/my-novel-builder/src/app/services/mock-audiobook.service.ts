import { inject, Injectable } from '@angular/core';
import { forkJoin, map, of, throwError } from 'rxjs';
import { AudiobookService } from './audiobook.service';
import { IntegrationsService } from './integrations.service';
import { NovelService } from './novel.service';
import { AudiobookJob, AudiobookPreview, AudiobookSettings } from '../types/dtos/generate/audiobook.dto';
import { v4 as uuid } from 'uuid';

/** In-memory demo jobs; no external generation. Downloads are a clearly labelled sample ZIP. */
@Injectable()
export class MockAudiobookService extends AudiobookService {
  override readonly isDemo = true;
  private readonly integrations = inject(IntegrationsService);
  private readonly novels = inject(NovelService);
  private previews = new Map<string, { novelId: string; preview: AudiobookPreview; sections: AudiobookJob['sections'] }>();
  private jobs = new Map<string, { job: AudiobookJob; key: string; snapshotId: string; readyAt: number }>();
  /** @inheritdoc */
  preview(novelId: string, settings: AudiobookSettings) {
    return forkJoin([this.integrations.getIntegrationsConfig(), this.novels.getNovelProse(novelId)]).pipe(map(([config, prose]) => {
      const sections: AudiobookJob['sections'] = [];
      let characters = 0;
      let words = 0;
      let skippedChapters = 0;
      prose.chapters.forEach((chapter, chapterIndex) => {
        let spoken = false;
        chapter.sections.forEach((section, sectionIndex) => {
          const text = new DOMParser().parseFromString(section.text || '', 'text/html').body.textContent?.trim() || '';
          spoken ||= text.length > 0;
          characters += text.length;
          words += text ? text.split(/\s+/).length : 0;
          sections.push({ chapterIndex, sectionIndex, state: text ? 'pending' : 'skipped' });
        });
        if (!spoken) skippedChapters++;
      });
      if (!characters) throw new Error('The novel has no spoken prose. Add prose before generating an audiobook.');
      const preview: AudiobookPreview = {
        snapshotId: uuid(), snapshotAt: new Date().toISOString(), expiresAt: new Date(Date.now() + 900000).toISOString(),
        settings: { provider: config.ttsProvider, modelId: config.ttsModelId, voiceId: config.ttsVoiceId,
          enableTextEmphasis: config.ttsEnableTextEmphasis, enableImmersive: config.ttsEnableImmersive,
          immersivePauseMs: config.ttsImmersivePauseMs, textGenerationProvider: config.textGenerationProvider,
          textGenerationModelId: config.textGenerationModelId, ...settings },
        totalCharacters: characters, spokenWords: words,
        eligibleChapters: prose.chapters.length - skippedChapters,
        eligibleSections: sections.filter(s => s.state !== 'skipped').length,
        skippedSections: sections.filter(s => s.state === 'skipped').length,
        skippedChapters, reusableSections: 0, charactersNeedingSynthesis: characters,
        warnings: ['Demo only: generation is simulated. The download contains sample silence, not your novel.'],
      };
      this.previews.set(preview.snapshotId, { novelId, preview, sections });
      return preview;
    }));
  }
  /** @inheritdoc */
  start(novelId: string, snapshotId: string, key: string) {
    const existing = [...this.jobs.values()].find(v => v.job.novelId === novelId && v.key === key);
    if (existing) return existing.snapshotId === snapshotId ? of(structuredClone(existing.job)) : throwError(() => new Error('Submission key belongs to another preview.'));
    const saved = this.previews.get(snapshotId);
    if (!saved || saved.novelId !== novelId || Date.parse(saved.preview.expiresAt) <= Date.now()) return throwError(() => new Error('Refresh the expired preview.'));
    if ([...this.jobs.values()].some(v => v.job.novelId === novelId && ['queued', 'running', 'cancelling'].includes(v.job.state))) return throwError(() => new Error('This novel already has an active job.'));
    const job: AudiobookJob = { id: uuid(), novelId, snapshotAt: saved.preview.snapshotAt, state: 'queued', stage: 'queued', attemptNumber: 1,
      completedSections: 0, reusedSections: 0, generatedSections: 0, failedSections: 0, skippedSections: saved.preview.skippedSections,
      completedChapters: 0, totalChapters: new Set(saved.sections.filter(s => s.state !== 'skipped').map(s => s.chapterIndex)).size,
      downloadAvailable: false, errorCode: null, sections: structuredClone(saved.sections), attempts: [] };
    this.jobs.set(job.id, { job, key, snapshotId, readyAt: Date.now() + 4000 });
    return of(structuredClone(job));
  }
  /** @inheritdoc */
  list(novelId: string) {
    const jobs = [...this.jobs.values()].filter(v => v.job.novelId === novelId);
    jobs.forEach(v => {
      if (v.job.state === 'queued' || v.job.state === 'running') {
        v.job.state = Date.now() >= v.readyAt ? 'completed' : 'running';
        v.job.stage = v.job.state === 'completed' ? 'ready' : 'rendering';
        if (v.job.state === 'completed') {
          v.job.sections.forEach(s => { if (s.state !== 'skipped') s.state = 'generated'; });
          v.job.completedSections = v.job.generatedSections = v.job.sections.filter(s => s.state !== 'skipped').length;
          v.job.completedChapters = v.job.totalChapters;
          v.job.downloadAvailable = true;
        }
      }
    });
    return of(structuredClone(jobs.map(v => v.job).reverse()));
  }
  /** @inheritdoc */
  cancel(novelId: string, jobId: string) {
    const saved = this.jobs.get(jobId);
    if (!saved || saved.job.novelId !== novelId) return throwError(() => new Error('Job not found.'));
    if (['queued', 'running', 'cancelling'].includes(saved.job.state)) saved.job.state = 'cancelled';
    return of(structuredClone(saved.job));
  }
  /** @inheritdoc */
  retry(novelId: string, jobId: string) {
    const saved = this.jobs.get(jobId);
    if (!saved || saved.job.novelId !== novelId || !['failed', 'cancelled'].includes(saved.job.state)) return throwError(() => new Error('Job cannot be retried.'));
    saved.job.state = 'queued'; saved.job.stage = 'queued'; saved.job.attemptNumber++;
    saved.readyAt = Date.now() + 4000;
    return of(structuredClone(saved.job));
  }
  /** @inheritdoc */
  downloadUrl(): string { return 'assets/mock-audiobook.zip'; }
}
