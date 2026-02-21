import { HttpEvent, HttpResponse } from '@angular/common/http';
import { Observable, Subscriber } from 'rxjs';
import { ImageGenerationModelInfoDto } from '../../types/dtos/generate/image-generation-model-info.dto';

export function mockedImageGenerationResponse(): Observable<HttpEvent<Blob>> {
  return new Observable<HttpEvent<Blob>>((subscriber: Subscriber<HttpEvent<Blob>>) => {
    const blob = new Blob(['mock-image-data'], { type: 'image/png' });
    const response = new HttpResponse({
      body: blob,
      status: 200,
      statusText: 'OK',
    });
    subscriber.next(response);
    subscriber.complete();
  });
}

export const mockedImageModelInfos: ImageGenerationModelInfoDto[] = [
  {
    modelId: 'z-image/turbo',
    name: 'Z-Image Turbo',
    isImageEditor: false,
  },
  {
    modelId: 'z-image/hd',
    name: 'Z-Image HD',
    isImageEditor: true,
  },
];
