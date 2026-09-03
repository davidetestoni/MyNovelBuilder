import { HttpEvent, HttpResponse } from '@angular/common/http';
import { Observable, Subscriber } from 'rxjs';
import { ImageGenerationModelInfoDto } from '../../types/dtos/generate/image-generation-model-info.dto';

export function mockedImageGenerationResponse(
  mimeType = 'image/png',
): Observable<HttpEvent<Blob>> {
  return new Observable<HttpEvent<Blob>>((subscriber: Subscriber<HttpEvent<Blob>>) => {
    const blob = new Blob(['mock-image-data'], { type: mimeType });
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
    supportsImageGeneration: true,
    supportsImageEditing: false,
  },
  {
    modelId: 'z-image/hd',
    name: 'Z-Image HD',
    supportsImageGeneration: true,
    supportsImageEditing: true,
  },
];
