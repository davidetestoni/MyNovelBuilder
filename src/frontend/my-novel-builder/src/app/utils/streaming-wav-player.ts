// From: https://github.com/kyutai-labs/pocket-tts/blob/main/pocket_tts/static/index.html

export class StreamingWavPlayer {
  private audioContext: AudioContext;
  private sampleRate: number = 0;
  private numChannels: number = 0;
  private audioFormat: number = 0;
  private bitsPerSample: number = 0;
  private bytesPerFrame: number = 0;
  private headerParsed: boolean = false;
  private pendingData: Uint8Array = new Uint8Array(0);
  private nextStartTime: number = 0;
  private isPlaying: boolean = false;
  private minBufferSize: number = 16384;
  private pcmData: Uint8Array = new Uint8Array(0);
  private firstAudioPlayed: boolean = false;
  private firstAudioCallback?: () => void;

  constructor(firstAudioCallback?: () => void) {
    this.audioContext = new (
      window.AudioContext || (window as any).webkitAudioContext
    )();
    this.firstAudioCallback = firstAudioCallback;
  }

  private concatUint8Arrays(a: Uint8Array, b: Uint8Array): Uint8Array {
    const merged = new Uint8Array(a.length + b.length);
    merged.set(a, 0);
    merged.set(b, a.length);
    return merged;
  }

  private readFourCc(data: Uint8Array, offset: number): string {
    return String.fromCharCode(
      data[offset],
      data[offset + 1],
      data[offset + 2],
      data[offset + 3],
    );
  }

  private tryParseWavHeader(): void {
    if (this.pendingData.length < 12) {
      return;
    }

    const view = new DataView(
      this.pendingData.buffer,
      this.pendingData.byteOffset,
      this.pendingData.byteLength,
    );

    const riff = this.readFourCc(this.pendingData, 0);
    const wave = this.readFourCc(this.pendingData, 8);
    if (riff !== 'RIFF' || wave !== 'WAVE') {
      throw new Error('Invalid WAV file');
    }

    let offset = 12;
    let foundFmt = false;
    let dataOffset = -1;

    while (offset + 8 <= this.pendingData.length) {
      const chunkId = this.readFourCc(this.pendingData, offset);
      const chunkSize = view.getUint32(offset + 4, true);
      const chunkDataOffset = offset + 8;
      const chunkEnd = chunkDataOffset + chunkSize;

      // The data chunk can be very large and arrives incrementally while
      // streaming; we only need its header to begin playback.
      if (chunkId === 'data') {
        dataOffset = chunkDataOffset;
        if (foundFmt) {
          break;
        }

        // Cannot safely skip the data payload to look for later chunks.
        return;
      }

      if (chunkId === 'fmt ') {
        if (chunkEnd > this.pendingData.length) {
          return;
        }

        if (chunkSize < 16) {
          throw new Error('Invalid WAV fmt chunk');
        }

        this.audioFormat = view.getUint16(chunkDataOffset, true);
        this.numChannels = view.getUint16(chunkDataOffset + 2, true);
        this.sampleRate = view.getUint32(chunkDataOffset + 4, true);
        this.bytesPerFrame = view.getUint16(chunkDataOffset + 12, true);
        this.bitsPerSample = view.getUint16(chunkDataOffset + 14, true);
        foundFmt = true;
      } else {
        if (chunkEnd > this.pendingData.length) {
          return;
        }
      }

      // WAV chunks are word-aligned.
      offset = chunkEnd + (chunkSize % 2);
    }

    if (!foundFmt || dataOffset < 0) {
      return;
    }

    const isPcm16 = this.audioFormat === 1 && this.bitsPerSample === 16;
    const isFloat32 = this.audioFormat === 3 && this.bitsPerSample === 32;
    if (!isPcm16 && !isFloat32) {
      throw new Error(
        `Unsupported WAV format: format=${this.audioFormat}, bitsPerSample=${this.bitsPerSample}`,
      );
    }

    if (this.bytesPerFrame <= 0) {
      throw new Error('Invalid WAV block alignment');
    }

    this.appendPcmData(this.pendingData.slice(dataOffset));
    this.pendingData = new Uint8Array(0);
    this.headerParsed = true;
  }

  private appendPcmData(newData: Uint8Array): void {
    const newBuffer = new Uint8Array(this.pcmData.length + newData.length);
    newBuffer.set(this.pcmData);
    newBuffer.set(newData, this.pcmData.length);
    this.pcmData = newBuffer;
  }

  private tryPlayBuffer(): void {
    if (!this.headerParsed || this.pcmData.length < this.minBufferSize) {
      return;
    }

    const samplesToPlay = Math.floor(this.pcmData.length / this.bytesPerFrame);
    const bytesToPlay = samplesToPlay * this.bytesPerFrame;

    if (bytesToPlay === 0) return;

    const dataToPlay = this.pcmData.slice(0, bytesToPlay);
    this.pcmData = this.pcmData.slice(bytesToPlay);

    const audioBuffer = this.audioContext.createBuffer(
      this.numChannels,
      samplesToPlay,
      this.sampleRate,
    );

    for (let channel = 0; channel < this.numChannels; channel++) {
      const channelData = audioBuffer.getChannelData(channel);

      if (this.audioFormat === 1 && this.bitsPerSample === 16) {
        const int16Data = new Int16Array(
          dataToPlay.buffer,
          dataToPlay.byteOffset,
          samplesToPlay * this.numChannels,
        );

        for (let i = 0; i < samplesToPlay; i++) {
          channelData[i] = int16Data[i * this.numChannels + channel] / 32768;
        }
      } else if (this.audioFormat === 3 && this.bitsPerSample === 32) {
        const floatData = new Float32Array(
          dataToPlay.buffer,
          dataToPlay.byteOffset,
          samplesToPlay * this.numChannels,
        );

        for (let i = 0; i < samplesToPlay; i++) {
          const sample = floatData[i * this.numChannels + channel];
          channelData[i] = Math.max(-1, Math.min(1, sample));
        }
      }
    }

    const source = this.audioContext.createBufferSource();
    source.buffer = audioBuffer;
    source.connect(this.audioContext.destination);

    const currentTime = this.audioContext.currentTime;
    const startTime = Math.max(currentTime, this.nextStartTime);

    source.start(startTime);

    // Track first audio playback
    if (!this.firstAudioPlayed && this.firstAudioCallback) {
      this.firstAudioPlayed = true;
      this.firstAudioCallback();
    }

    this.nextStartTime = startTime + audioBuffer.duration;
    this.isPlaying = true;

    if (this.pcmData.length >= this.minBufferSize) {
      setTimeout(() => this.tryPlayBuffer(), 10);
    }
  }

  public addChunk(chunk: Uint8Array): void {
    if (!this.headerParsed) {
      this.pendingData = this.concatUint8Arrays(this.pendingData, chunk);
      this.tryParseWavHeader();
    } else {
      this.appendPcmData(chunk);
    }

    this.tryPlayBuffer();
  }

  public stop(): void {
    this.audioContext.close();
    this.isPlaying = false;
  }

  public getIsPlaying(): boolean {
    return this.isPlaying;
  }
}
