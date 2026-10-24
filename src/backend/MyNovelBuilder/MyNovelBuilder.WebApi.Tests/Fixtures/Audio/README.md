These MP3 fixtures contain a generated 440 Hz sine wave lasting 200 ms.
They cover 24 kHz mono, 44.1 kHz mono, and 48 kHz stereo without network calls
or a runtime dependency on FFmpeg. They contain no recorded speech.

Generate each fixture with FFmpeg, replacing RATE and CHANNELS:

```sh
ffmpeg -f lavfi -i sine=frequency=440:sample_rate=RATE:duration=0.2 -ac CHANNELS -c:a libmp3lame -b:a 64k -map_metadata -1 -write_xing 0 tone-RATE-LAYOUT.mp3
```
