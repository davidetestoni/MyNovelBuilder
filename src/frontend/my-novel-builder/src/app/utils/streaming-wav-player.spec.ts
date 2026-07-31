import { StreamingWavPlayer } from './streaming-wav-player';

interface MockAudioBuffer {
  channelData: Float32Array[];
  duration: number;
  getChannelData: jasmine.Spy;
}

interface MockBufferSource {
  buffer: AudioBuffer | null;
  connect: jasmine.Spy;
  start: jasmine.Spy;
}

interface MockAudioContext {
  currentTime: number;
  destination: AudioDestinationNode;
  createBuffer: jasmine.Spy;
  createBufferSource: jasmine.Spy;
  close: jasmine.Spy;
}

interface WavOptions {
  format?: number;
  channels?: number;
  sampleRate?: number;
  bitsPerSample?: number;
  blockAlign?: number;
  fmtSize?: number;
  extraChunks?: Uint8Array[];
}

describe('StreamingWavPlayer', () => {
  let audioContext: MockAudioContext;
  let audioBuffers: MockAudioBuffer[];
  let sources: MockBufferSource[];

  const concatenate = (...arrays: Uint8Array[]): Uint8Array => {
    const result = new Uint8Array(
      arrays.reduce((length, array) => length + array.length, 0),
    );
    let offset = 0;
    for (const array of arrays) {
      result.set(array, offset);
      offset += array.length;
    }
    return result;
  };

  const fourCc = (value: string): Uint8Array =>
    Uint8Array.from(value, (character) => character.charCodeAt(0));

  const chunk = (id: string, data: Uint8Array): Uint8Array => {
    const header = new Uint8Array(8);
    header.set(fourCc(id));
    new DataView(header.buffer).setUint32(4, data.length, true);
    return concatenate(
      header,
      data,
      data.length % 2 === 0 ? new Uint8Array(0) : new Uint8Array(1),
    );
  };

  const pcm16 = (...samples: number[]): Uint8Array => {
    const result = new Uint8Array(samples.length * 2);
    const view = new DataView(result.buffer);
    samples.forEach((sample, index) => view.setInt16(index * 2, sample, true));
    return result;
  };

  const float32 = (...samples: number[]): Uint8Array => {
    const result = new Uint8Array(samples.length * 4);
    const view = new DataView(result.buffer);
    samples.forEach((sample, index) =>
      view.setFloat32(index * 4, sample, true),
    );
    return result;
  };

  const wav = (pcm: Uint8Array, options: WavOptions = {}): Uint8Array => {
    const format = options.format ?? 1;
    const channels = options.channels ?? 1;
    const sampleRate = options.sampleRate ?? 8000;
    const bitsPerSample = options.bitsPerSample ?? 16;
    const fmtSize = options.fmtSize ?? 16;
    const fmt = new Uint8Array(fmtSize);
    const fmtView = new DataView(fmt.buffer);
    if (fmtSize >= 16) {
      const blockAlign =
        options.blockAlign ?? channels * Math.ceil(bitsPerSample / 8);
      fmtView.setUint16(0, format, true);
      fmtView.setUint16(2, channels, true);
      fmtView.setUint32(4, sampleRate, true);
      fmtView.setUint32(8, sampleRate * blockAlign, true);
      fmtView.setUint16(12, blockAlign, true);
      fmtView.setUint16(14, bitsPerSample, true);
    }

    const body = concatenate(
      chunk('fmt ', fmt),
      ...(options.extraChunks ?? []),
      chunk('data', pcm),
    );
    const header = new Uint8Array(12);
    header.set(fourCc('RIFF'));
    new DataView(header.buffer).setUint32(4, body.length + 4, true);
    header.set(fourCc('WAVE'), 8);
    return concatenate(header, body);
  };

  const createPlayer = (callback?: () => void): StreamingWavPlayer => {
    const player = new StreamingWavPlayer(callback);
    (player as unknown as { minBufferSize: number }).minBufferSize = 1;
    return player;
  };

  beforeEach(() => {
    audioBuffers = [];
    sources = [];
    audioContext = {
      currentTime: 1,
      destination: {} as AudioDestinationNode,
      createBuffer: jasmine.createSpy('createBuffer').and.callFake(
        (channels: number, length: number, sampleRate: number) => {
          const channelData = Array.from(
            { length: channels },
            () => new Float32Array(length),
          );
          const buffer: MockAudioBuffer = {
            channelData,
            duration: length / sampleRate,
            getChannelData: jasmine
              .createSpy('getChannelData')
              .and.callFake((channel: number) => channelData[channel]),
          };
          audioBuffers.push(buffer);
          return buffer as unknown as AudioBuffer;
        },
      ),
      createBufferSource: jasmine
        .createSpy('createBufferSource')
        .and.callFake(() => {
          const source: MockBufferSource = {
            buffer: null,
            connect: jasmine.createSpy('connect'),
            start: jasmine.createSpy('start'),
          };
          sources.push(source);
          return source as unknown as AudioBufferSourceNode;
        }),
      close: jasmine.createSpy('close'),
    };
    spyOn(window, 'AudioContext').and.returnValue(
      audioContext as unknown as AudioContext,
    );
  });

  it('waits for a complete header before creating audio', () => {
    const player = createPlayer();
    const data = wav(pcm16(1000));

    player.addChunk(data.slice(0, 11));
    expect(audioContext.createBuffer).not.toHaveBeenCalled();

    player.addChunk(data.slice(11, 30));
    expect(audioContext.createBuffer).not.toHaveBeenCalled();

    player.addChunk(data.slice(30));
    expect(audioContext.createBuffer).toHaveBeenCalledTimes(1);
  });

  it('buffers PCM data until its configured minimum is available', () => {
    const player = new StreamingWavPlayer();

    player.addChunk(wav(pcm16(1000)));

    expect(audioContext.createBuffer).not.toHaveBeenCalled();
    expect(player.getIsPlaying()).toBeFalse();
  });

  it('decodes interleaved 16-bit PCM channels and schedules playback', () => {
    const firstAudio = jasmine.createSpy('firstAudio');
    const player = createPlayer(firstAudio);

    player.addChunk(wav(pcm16(-32768, 16384, 32767, -16384), { channels: 2 }));

    expect(audioContext.createBuffer).toHaveBeenCalledOnceWith(2, 2, 8000);
    expect(Array.from(audioBuffers[0].channelData[0])).toEqual([
      -1,
      32767 / 32768,
    ]);
    expect(Array.from(audioBuffers[0].channelData[1])).toEqual([0.5, -0.5]);
    expect(sources[0].connect).toHaveBeenCalledOnceWith(audioContext.destination);
    expect(sources[0].start).toHaveBeenCalledOnceWith(1);
    expect(firstAudio).toHaveBeenCalledTimes(1);
    expect(player.getIsPlaying()).toBeTrue();
  });

  it('decodes float PCM and clamps samples to the valid audio range', () => {
    const player = createPlayer();

    player.addChunk(
      wav(float32(-1.5, -0.25, 0.5, 2), {
        format: 3,
        bitsPerSample: 32,
        blockAlign: 4,
      }),
    );

    expect(Array.from(audioBuffers[0].channelData[0])).toEqual([
      -1,
      -0.25,
      0.5,
      1,
    ]);
  });

  it('skips unknown word-aligned chunks before the audio data', () => {
    const player = createPlayer();
    const metadata = chunk('JUNK', Uint8Array.of(1, 2, 3));

    player.addChunk(wav(pcm16(4096), { extraChunks: [metadata] }));

    expect(Array.from(audioBuffers[0].channelData[0])).toEqual([0.125]);
  });

  it('keeps incomplete frames for a later chunk', () => {
    const player = createPlayer();
    player.addChunk(wav(new Uint8Array(0)));

    player.addChunk(Uint8Array.of(0, 64, 0));
    expect(Array.from(audioBuffers[0].channelData[0])).toEqual([0.5]);

    player.addChunk(Uint8Array.of(32));
    expect(Array.from(audioBuffers[1].channelData[0])).toEqual([0.25]);
  });

  it('schedules consecutive buffers without overlapping them', () => {
    const firstAudio = jasmine.createSpy('firstAudio');
    const player = createPlayer(firstAudio);
    player.addChunk(wav(pcm16(1000), { sampleRate: 10 }));

    audioContext.currentTime = 1.05;
    player.addChunk(pcm16(2000));

    expect(sources[0].start).toHaveBeenCalledOnceWith(1);
    expect(sources[1].start).toHaveBeenCalledOnceWith(1.1);
    expect(firstAudio).toHaveBeenCalledTimes(1);
  });

  it('uses the current audio time after a gap between buffers', () => {
    const player = createPlayer();
    player.addChunk(wav(pcm16(1000), { sampleRate: 10 }));

    audioContext.currentTime = 5;
    player.addChunk(pcm16(2000));

    expect(sources[1].start).toHaveBeenCalledOnceWith(5);
  });

  it('rejects data that is not a WAV file', () => {
    const player = createPlayer();
    const invalid = wav(pcm16(0));
    invalid.set(fourCc('NOPE'));

    expect(() => player.addChunk(invalid)).toThrowError('Invalid WAV file');
  });

  it('rejects a truncated fmt chunk definition', () => {
    const player = createPlayer();

    expect(() => player.addChunk(wav(pcm16(0), { fmtSize: 12 }))).toThrowError(
      'Invalid WAV fmt chunk',
    );
  });

  it('rejects unsupported WAV encodings', () => {
    const player = createPlayer();

    expect(() =>
      player.addChunk(wav(Uint8Array.of(0), { format: 6, bitsPerSample: 8 })),
    ).toThrowError('Unsupported WAV format: format=6, bitsPerSample=8');
  });

  it('rejects a zero block alignment', () => {
    const player = createPlayer();

    expect(() =>
      player.addChunk(wav(pcm16(0), { blockAlign: 0 })),
    ).toThrowError('Invalid WAV block alignment');
  });

  it('closes the audio context and clears playback state when stopped', () => {
    const player = createPlayer();
    player.addChunk(wav(pcm16(1000)));

    player.stop();

    expect(audioContext.close).toHaveBeenCalledTimes(1);
    expect(player.getIsPlaying()).toBeFalse();
  });

  it('defers a trailing partial frame', () => {
    const player = createPlayer();
    player.addChunk(
      wav(new Uint8Array(0), { channels: 2, blockAlign: 4 }),
    );

    player.addChunk(Uint8Array.of(0, 1, 2));

    expect(audioContext.createBuffer).not.toHaveBeenCalled();
  });
});
