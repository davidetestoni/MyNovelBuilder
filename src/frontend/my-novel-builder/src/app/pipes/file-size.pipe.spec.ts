import { FileSizePipe } from './file-size.pipe';

describe('FileSizePipe', () => {
  const pipe = new FileSizePipe();

  it('formats bytes below one kilobyte', () => {
    expect(pipe.transform(0)).toBe('0 B');
    expect(pipe.transform(512.4)).toBe('512 B');
    expect(pipe.transform(512.6)).toBe('513 B');
  });

  it('formats kilobytes with one decimal below ten', () => {
    expect(pipe.transform(1024)).toBe('1.0 KB');
    expect(pipe.transform(9728)).toBe('9.5 KB');
  });

  it('formats larger unit values without unnecessary decimals', () => {
    expect(pipe.transform(10 * 1024)).toBe('10 KB');
    expect(pipe.transform(25 * 1024 * 1024)).toBe('25 MB');
  });

  it('selects each supported unit at its boundary', () => {
    expect(pipe.transform(1024 ** 2)).toBe('1.0 MB');
    expect(pipe.transform(1024 ** 3)).toBe('1.0 GB');
    expect(pipe.transform(1024 ** 4)).toBe('1.0 TB');
  });

  it('keeps values above a terabyte in the largest supported unit', () => {
    expect(pipe.transform(1024 ** 5)).toBe('1024 TB');
  });

  it('returns an empty string for absent, negative, or non-finite sizes', () => {
    expect(pipe.transform(null)).toBe('');
    expect(pipe.transform(undefined)).toBe('');
    expect(pipe.transform(-1)).toBe('');
    expect(pipe.transform(Number.NaN)).toBe('');
    expect(pipe.transform(Number.POSITIVE_INFINITY)).toBe('');
  });
});
