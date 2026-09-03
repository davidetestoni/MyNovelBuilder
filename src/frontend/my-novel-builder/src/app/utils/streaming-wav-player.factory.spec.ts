import { TestBed } from '@angular/core/testing';
import { StreamingWavPlayer } from './streaming-wav-player';
import { STREAMING_WAV_PLAYER_FACTORY } from './streaming-wav-player.factory';

describe('STREAMING_WAV_PLAYER_FACTORY', () => {
  it('creates independent streaming player handles', () => {
    const close = jasmine.createSpy('close');
    spyOn(window, 'AudioContext').and.returnValue(
      { close } as unknown as AudioContext,
    );
    const factory = TestBed.inject(STREAMING_WAV_PLAYER_FACTORY);

    const first = factory();
    const second = factory();

    expect(first).toBeInstanceOf(StreamingWavPlayer);
    expect(second).toBeInstanceOf(StreamingWavPlayer);
    expect(second).not.toBe(first);

    first.stop();
    expect(close).toHaveBeenCalledTimes(1);
  });
});
