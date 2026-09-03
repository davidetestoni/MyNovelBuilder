import { SpacedPipe } from './spaced.pipe';

describe('SpacedPipe', () => {
  const pipe = new SpacedPipe();

  it('turns camel case into capitalized words', () => {
    expect(pipe.transform('thirdPersonLimited')).toBe(
      'Third Person Limited',
    );
  });

  it('turns Pascal case into separate words', () => {
    expect(pipe.transform('GenerateText')).toBe('Generate Text');
  });

  it('preserves acronym groups while separating the following word', () => {
    expect(pipe.transform('HTTPResponseCode')).toBe('HTTP Response Code');
  });

  it('separates words after digits', () => {
    expect(pipe.transform('chapter2Summary')).toBe('Chapter2 Summary');
  });

  it('capitalizes an unspaced single word', () => {
    expect(pipe.transform('english')).toBe('English');
  });

  it('returns an empty string for absent text', () => {
    expect(pipe.transform('')).toBe('');
    expect(pipe.transform(null)).toBe('');
    expect(pipe.transform(undefined)).toBe('');
  });
});
