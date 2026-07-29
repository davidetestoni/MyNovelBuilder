import { InjectionToken } from '@angular/core';
import { StreamingWavPlayer } from './streaming-wav-player';

export interface StreamingWavPlayerHandle {
  addChunk(chunk: Uint8Array): void;
  stop(): void;
}

export type StreamingWavPlayerFactory = () => StreamingWavPlayerHandle;

export const STREAMING_WAV_PLAYER_FACTORY =
  new InjectionToken<StreamingWavPlayerFactory>(
    'StreamingWavPlayerFactory',
    {
      providedIn: 'root',
      factory: () => () => new StreamingWavPlayer(),
    },
  );
