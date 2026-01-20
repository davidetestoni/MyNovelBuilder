// From: https://github.com/kyutai-labs/pocket-tts/blob/main/pocket_tts/static/index.html

export class StreamingWavPlayer {
  private audioContext: AudioContext;
  private sampleRate: number = 0;
  private numChannels: number = 0;
  private headerParsed: boolean = false;
  private headerBuffer: Uint8Array = new Uint8Array(44);
  private headerBytesReceived: number = 0;
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

  private parseWavHeader(header: Uint8Array): void {
    const view = new DataView(header.buffer);

    const riff = String.fromCharCode(...Array.from(header.slice(0, 4)));
    const wave = String.fromCharCode(...Array.from(header.slice(8, 12)));

    if (riff !== 'RIFF' || wave !== 'WAVE') {
      throw new Error('Invalid WAV file');
    }

    this.numChannels = view.getUint16(22, true);
    this.sampleRate = view.getUint32(24, true);
    const bitsPerSample = view.getUint16(34, true);

    console.log(
      `WAV Format: ${this.sampleRate}Hz, ${this.numChannels} channels, ${bitsPerSample} bits`,
    );

    this.headerParsed = true;
  }

  private appendPcmData(newData: Uint8Array): void {
    const newBuffer = new Uint8Array(this.pcmData.length + newData.length);
    newBuffer.set(this.pcmData);
    newBuffer.set(newData, this.pcmData.length);
    this.pcmData = newBuffer;
  }

  private async tryPlayBuffer(): Promise<void> {
    if (!this.headerParsed || this.pcmData.length < this.minBufferSize) {
      return;
    }

    const bytesPerSample = this.numChannels * 2;
    const samplesToPlay = Math.floor(this.pcmData.length / bytesPerSample);
    const bytesToPlay = samplesToPlay * bytesPerSample;

    if (bytesToPlay === 0) return;

    const dataToPlay = this.pcmData.slice(0, bytesToPlay);
    this.pcmData = this.pcmData.slice(bytesToPlay);

    const audioBuffer = this.audioContext.createBuffer(
      this.numChannels,
      samplesToPlay,
      this.sampleRate,
    );

    const int16Data = new Int16Array(
      dataToPlay.buffer,
      dataToPlay.byteOffset,
      samplesToPlay * this.numChannels,
    );

    for (let channel = 0; channel < this.numChannels; channel++) {
      const channelData = audioBuffer.getChannelData(channel);
      for (let i = 0; i < samplesToPlay; i++) {
        channelData[i] = int16Data[i * this.numChannels + channel] / 32768;
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
      const headerBytesNeeded = 44 - this.headerBytesReceived;
      const bytesToCopy = Math.min(headerBytesNeeded, chunk.length);

      this.headerBuffer.set(
        chunk.slice(0, bytesToCopy),
        this.headerBytesReceived,
      );

      this.headerBytesReceived += bytesToCopy;

      if (this.headerBytesReceived >= 44) {
        this.parseWavHeader(this.headerBuffer);

        if (chunk.length > bytesToCopy) {
          this.appendPcmData(chunk.slice(bytesToCopy));
        }
      }
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
