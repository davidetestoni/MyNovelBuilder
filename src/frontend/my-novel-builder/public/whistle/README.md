# Whistle browser transcription

Upstream: https://github.com/cactus-compute/needle (Cactus Compute).
Runtime and weights are distributed under Apache-2.0; see `LICENSE`.
The three upstream assets below are tracked with Git LFS.

- `needle.js` and `needle.wasm`: unmodified files from
  https://huggingface.co/Cactus-Compute/needle3/tree/2ae11323dc000f5e70c49f7403efa6af12ba9e67/wasm
- `whistle.bin`: unmodified `whistle.cact`, renamed so ASP.NET's static file
  middleware serves it as binary data, from
  https://huggingface.co/Cactus-Compute/whistle/tree/b358ddadd89b7a713b5aa131f23032d3cca1b251

These assets are served by MyNovelBuilder and loaded only when transcription is
requested. Audio is processed in a dedicated browser worker. No third-party
requests or speech-to-text server are needed. The browser caches the model when
Cache Storage is available; cache failures do not prevent transcription.

The worker uses the official `needle.h` API: `needle_load` accepts the model
bytes and a 64-bit length; `needle_transcribe` accepts 16 kHz mono float PCM,
at most 30 seconds, and returns JSON. Keep the model's WASM allocation alive
until the worker terminates.
