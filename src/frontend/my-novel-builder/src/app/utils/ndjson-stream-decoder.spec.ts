import { NdjsonStreamDecoder } from './ndjson-stream-decoder';

interface TestRecord {
  content: string;
}

describe('NdjsonStreamDecoder', () => {
  it('decodes records added to a cumulative response', () => {
    const decoder = new NdjsonStreamDecoder<TestRecord>();

    expect(decoder.pushCumulative('{"content":"one"}\n')).toEqual([
      { content: 'one' },
    ]);
    expect(
      decoder.pushCumulative('{"content":"one"}\n{"content":"two"}\n'),
    ).toEqual([{ content: 'two' }]);
  });

  it('buffers an incomplete record until the remaining text arrives', () => {
    const decoder = new NdjsonStreamDecoder<TestRecord>();

    expect(decoder.pushCumulative('{"content":"hel')).toEqual([]);
    expect(decoder.pushCumulative('{"content":"hello"}\n')).toEqual([
      { content: 'hello' },
    ]);
  });

  it('flushes a final record without a trailing newline', () => {
    const decoder = new NdjsonStreamDecoder<TestRecord>();

    expect(decoder.pushCumulative('{"content":"done"}')).toEqual([]);
    expect(decoder.pushCumulative('{"content":"done"}', true)).toEqual([
      { content: 'done' },
    ]);
  });

  it('reports malformed completed records', () => {
    const decoder = new NdjsonStreamDecoder<TestRecord>();

    expect(() => decoder.pushCumulative('{"content":}\n')).toThrowError();
  });

  it('rejects a cumulative response that rewrites received text', () => {
    const decoder = new NdjsonStreamDecoder<TestRecord>();
    decoder.pushCumulative('{"content":"one"}\n');

    expect(() => decoder.pushCumulative('{"content":"other"}\n')).toThrowError(
      'The streamed response changed after it was received.',
    );
  });
});
