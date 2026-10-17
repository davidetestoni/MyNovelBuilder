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
    expect(request.request.body).toEqual({ snapshotId: 'snapshot', idempotencyKey: 'key' }); request.flush({});
    expect(service.downloadUrl('novel', 'job')).toBe('/api/novel/novel/audiobook/jobs/job/download');
  });
});
