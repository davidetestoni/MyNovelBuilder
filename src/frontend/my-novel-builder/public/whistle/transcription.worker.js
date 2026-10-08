/* MyNovelBuilder adapter for Cactus Compute's Apache-2.0 Whistle runtime. */
/* global createNeedle */
importScripts('needle.js');

async function getModel() {
  const url = new URL('whistle.bin', self.location.href).href;
  let cache;
  try {
    cache = await caches.open('mnb-whistle-b358ddadd89b7a713b5aa131f23032d3cca1b251');
    const saved = await cache.match(url);
    if (saved) return new Uint8Array(await saved.arrayBuffer());
  } catch {
    // Private browsing and storage quotas must not prevent transcription.
  }
  const response = await fetch(url);
  if (!response.ok) throw new Error('Could not load the transcription model. Please try again.');
  if (cache) {
    try {
      await cache.put(url, response.clone());
    } catch {
      // Model caching is optional.
    }
  }
  return new Uint8Array(await response.arrayBuffer());
}

self.onmessage = async ({ data }) => {
  try {
    self.postMessage({ status: 'Loading transcription model…' });
    const [wasmResponse, bytes] = await Promise.all([fetch('needle.wasm'), getModel()]);
    if (!wasmResponse.ok) throw new Error('Could not load the transcription engine. Please try again.');
    const runtime = await createNeedle({ wasmBinary: await wasmResponse.arrayBuffer() });
    const model = runtime._malloc(bytes.byteLength);
    if (!model) throw new Error('Not enough memory to load the transcription model.');
    runtime.HEAPU8.set(bytes, model);
    if (runtime._needle_load(model, BigInt(bytes.byteLength)) < 0) {
      throw new Error(runtime.UTF8ToString(runtime._needle_last_error()) || 'Could not initialize Whistle.');
    }

    self.postMessage({ status: 'Transcribing…' });
    const samples = data.samples;
    const pcm = runtime._malloc(samples.byteLength);
    const output = runtime._malloc(65536);
    if (!pcm || !output) throw new Error('Not enough memory to transcribe this sample.');
    try {
      runtime.HEAPU8.set(new Uint8Array(samples.buffer, samples.byteOffset, samples.byteLength), pcm);
      const result = runtime.ccall('needle_transcribe', 'number',
        ['number', 'number', 'string', 'number', 'number', 'number', 'number'],
        [pcm, samples.length, data.language, 0, 0, output, 65536]);
      if (result < 0) {
        throw new Error(runtime.UTF8ToString(runtime._needle_last_error()) || 'Transcription failed.');
      }
      const transcript = JSON.parse(runtime.UTF8ToString(output));
      if (typeof transcript.text !== 'string') throw new Error('Whistle returned an invalid transcript.');
      self.postMessage({ text: transcript.text.trim() });
    } finally {
      runtime._free(pcm);
      runtime._free(output);
    }
  } catch (error) {
    self.postMessage({ error: error instanceof Error ? error.message : 'Transcription failed.' });
  }
};
