import {
  HttpDownloadProgressEvent,
  HttpEvent,
  HttpEventType,
  HttpResponse,
} from '@angular/common/http';
import { Observable, Subscriber } from 'rxjs';
import { GenerateTextResponseChunkDto } from '../../types/dtos/generate/generate-text-response-chunk.dto';
import { TextGenerationModelInfoDto } from '../../types/dtos/generate/text-generation-model-info.dto';

export function mockedTextGenerationResponse(
  generatedText: string,
): Observable<HttpEvent<string>> {
  return new Observable<HttpEvent<string>>(
    (subscriber: Subscriber<HttpEvent<string>>) => {
      setTimeout(() => {
        let index = 0;
        let partialText = '';

        const intervalId = setInterval(() => {
          if (index < generatedText.length) {
            const char = generatedText.charAt(index);
            const chunk: GenerateTextResponseChunkDto = { content: char };

            partialText += JSON.stringify(chunk) + '\n';

            subscriber.next(<HttpDownloadProgressEvent>{
              type: HttpEventType.DownloadProgress,
              loaded: index + 1,
              total: generatedText.length,
              partialText,
            });

            index++;
          } else {
            clearInterval(intervalId);

            const finalResponse = new HttpResponse({
              body: partialText,
              status: 200,
              statusText: 'OK',
            });
            subscriber.next(finalResponse);
            subscriber.complete();
          }
        }, 50);
      }, 500);
    },
  );
}

export function mockedTextCompletionContent(): string {
  return JSON.stringify([
    {
      title: 'Mocked Story Event',
      date: 'Day 1',
      description: 'A mocked story event description.',
    },
  ]);
}

export const mockedTextGenerationModelInfos: TextGenerationModelInfoDto[] = [
  {
    id: 'mocked-model',
    isVisionCapable: true,
    supportsStructuredOutputs: true,
  },
];
