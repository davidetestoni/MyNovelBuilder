import { Observable } from 'rxjs';
import { TextGenerationModelInfoDto } from '../../types/dtos/generate/text-generation-model-info.dto';
import type { GenerateTextStreamUpdate } from '../generate-text.service';

export function mockedTextGenerationResponse(
  generatedText: string,
): Observable<GenerateTextStreamUpdate> {
  return new Observable<GenerateTextStreamUpdate>(
    (subscriber) => {
      let intervalId: ReturnType<typeof setInterval> | null = null;
      const timeoutId = setTimeout(() => {
        let index = 0;
        let content = '';

        intervalId = setInterval(() => {
          if (index < generatedText.length) {
            content += generatedText.charAt(index);
            subscriber.next({ content, isComplete: false });
            index++;
          } else {
            if (intervalId !== null) {
              clearInterval(intervalId);
            }
            intervalId = null;
            subscriber.next({ content, isComplete: true });
            subscriber.complete();
          }
        }, 50);
      }, 500);

      return () => {
        clearTimeout(timeoutId);
        if (intervalId !== null) {
          clearInterval(intervalId);
        }
      };
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
    inputTokenPrice: 0.0000002,
    outputTokenPrice: 0.000015,
  },
];
