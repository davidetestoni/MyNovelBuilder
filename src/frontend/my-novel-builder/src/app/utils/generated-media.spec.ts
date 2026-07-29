import {
  createGeneratedMediaFile,
  extensionForGeneratedMediaMimeType,
  isVideoMimeType,
} from './generated-media';

describe('generated media utilities', () => {
  it('maps supported image and video MIME types to extensions', () => {
    expect(extensionForGeneratedMediaMimeType('video/mp4')).toBe('mp4');
    expect(extensionForGeneratedMediaMimeType('image/jpeg')).toBe('jpg');
    expect(extensionForGeneratedMediaMimeType('image/webp')).toBe('webp');
    expect(extensionForGeneratedMediaMimeType('image/gif')).toBe('gif');
  });

  it('uses PNG for unknown MIME types', () => {
    expect(extensionForGeneratedMediaMimeType('image/avif')).toBe('png');
    expect(extensionForGeneratedMediaMimeType('')).toBe('png');
  });

  it('creates a named file while preserving content and MIME type', async () => {
    const blob = new Blob(['generated'], { type: 'video/mp4' });

    const file = createGeneratedMediaFile(blob, 'scene');

    expect(file.name).toBe('scene.mp4');
    expect(file.type).toBe('video/mp4');
    expect(await file.text()).toBe('generated');
  });

  it('defaults untyped generated blobs to PNG files', () => {
    const file = createGeneratedMediaFile(new Blob(['generated']));

    expect(file.name).toBe('generated-media.png');
    expect(file.type).toBe('image/png');
  });

  it('recognizes video MIME types without treating images as video', () => {
    expect(isVideoMimeType('video/mp4')).toBeTrue();
    expect(isVideoMimeType('video/webm')).toBeTrue();
    expect(isVideoMimeType('image/png')).toBeFalse();
  });

  it('handles absent MIME types defensively', () => {
    expect(isVideoMimeType(null)).toBeFalse();
    expect(isVideoMimeType(undefined)).toBeFalse();
  });
});
