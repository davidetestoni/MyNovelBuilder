import { HttpEvent, HttpResponse } from '@angular/common/http';
import { Observable, Subscriber } from 'rxjs';
import { VideoGenerationModelInfoDto } from '../../types/dtos/generate/video-generation-model-info.dto';

export function mockedVideoGenerationResponse(): Observable<HttpEvent<Blob>> {
  return new Observable<HttpEvent<Blob>>(
    (subscriber: Subscriber<HttpEvent<Blob>>) => {
      const blob = new Blob(['mock-video-data'], { type: 'video/mp4' });
      const response = new HttpResponse({
        body: blob,
        status: 200,
        statusText: 'OK',
      });
      subscriber.next(response);
      subscriber.complete();
    },
  );
}

export const mockedVideoModelInfos: VideoGenerationModelInfoDto[] = [
  {
    modelId: 'seedance/pro',
    name: 'Seedance Pro',
    supportsTextToVideo: true,
    supportsImageToVideo: true,
  },
];
