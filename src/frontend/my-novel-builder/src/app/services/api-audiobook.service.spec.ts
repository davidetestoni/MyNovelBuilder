import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ApiAudiobookService } from './api-audiobook.service';

describe('ApiAudiobookService', () => {
  let service: ApiAudiobookService;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), ApiAudiobookService] });
    service = TestBed.inject(ApiAudiobookService); http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  it('sends explicit false/zero overrides without altering them', () => {
    service.preview('novel', { enableImmersive: false, immersivePauseMs: 0 }).subscribe();
    const request = http.expectOne('/api/novel/novel/audiobook/preview');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ enableImmersive: false, immersivePauseMs: 0 }); request.flush({});
  });
  it('sends the snapshot and idempotency key and provides a streamed download URL', () => {
    service.start('novel', 'snapshot', 'key').subscribe();
    const request = http.expectOne('/api/novel/novel/audiobook/jobs');
    expect(request.request.body).toEqual({ snapshotId: 'snapshot', idempotencyKey: 'key', maxParallelSections: 1 }); request.flush({});
    expect(service.downloadUrl('novel', 'job')).toBe('/api/novel/novel/audiobook/jobs/job/download');
  });
  it('sends selected concurrency for creation and retry while allowing retries to retain their saved value', () => {
    service.start('novel', 'snapshot', 'key', 3).subscribe();
    const start = http.expectOne('/api/novel/novel/audiobook/jobs');
    expect(start.request.body.maxParallelSections).toBe(3); start.flush({});
    service.retry('novel', 'job', 2).subscribe();
    const retry = http.expectOne('/api/novel/novel/audiobook/jobs/job/retry');
    expect(retry.request.body).toEqual({ maxParallelSections: 2 }); retry.flush({});
    service.retry('novel', 'job').subscribe();
    const retained = http.expectOne('/api/novel/novel/audiobook/jobs/job/retry');
    expect(retained.request.body).toEqual({}); retained.flush({});
  });
});
