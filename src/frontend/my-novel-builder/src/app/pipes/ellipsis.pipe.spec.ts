import { EllipsisPipe } from './ellipsis.pipe';

describe('EllipsisPipe', () => {
  const pipe = new EllipsisPipe();

  it('returns text that fits within the requested length unchanged', () => {
    expect(pipe.transform('Short text', 20)).toBe('Short text');
    expect(pipe.transform('Exact', 5)).toBe('Exact');
  });

  it('truncates longer text and appends an ellipsis', () => {
    expect(pipe.transform('A longer sentence', 8)).toBe('A longer...');
  });

  it('uses the integer portion of fractional lengths', () => {
    expect(pipe.transform('abcdef', 3.9)).toBe('abc...');
  });

  it('treats negative lengths as zero', () => {
    expect(pipe.transform('text', -4)).toBe('...');
  });

  it('returns text unchanged for non-finite lengths', () => {
    expect(pipe.transform('text', Number.NaN)).toBe('text');
    expect(pipe.transform('text', Number.POSITIVE_INFINITY)).toBe('text');
  });

  it('returns an empty string for absent text', () => {
    expect(pipe.transform('', 5)).toBe('');
    expect(pipe.transform(null, 5)).toBe('');
    expect(pipe.transform(undefined, 5)).toBe('');
  });
});
