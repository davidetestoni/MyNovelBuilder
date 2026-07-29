import {
  extractImageFileFromClipboardData,
  readImageFileFromClipboard,
} from './clipboard-image';

describe('clipboard image utilities', () => {
  const originalClipboard = navigator.clipboard;

  const setClipboard = (clipboard: Partial<Clipboard>): void => {
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: clipboard,
    });
  };

  afterEach(() => {
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: originalClipboard,
    });
  });

  it('returns null for missing clipboard data or non-image items', () => {
    expect(extractImageFileFromClipboardData(null)).toBeNull();
    expect(extractImageFileFromClipboardData(undefined)).toBeNull();

    const items = [
      {
        kind: 'string',
        type: 'text/plain',
        getAsFile: () => null,
      },
    ] as unknown as DataTransferItemList;

    expect(extractImageFileFromClipboardData(items)).toBeNull();
  });

  it('returns the first image file from pasted data', () => {
    const image = new File(['pixels'], 'pasted.webp', { type: 'image/webp' });
    const items = [
      {
        kind: 'file',
        type: 'text/plain',
        getAsFile: () => new File(['text'], 'notes.txt'),
      },
      {
        kind: 'file',
        type: 'image/webp',
        getAsFile: () => image,
      },
    ] as unknown as DataTransferItemList;

    expect(extractImageFileFromClipboardData(items)).toBe(image);
  });

  it('creates a useful file name when pasted image data has none', () => {
    const unnamed = new File(['pixels'], '', { type: 'image/jpeg' });
    const items = [
      {
        kind: 'file',
        type: 'image/jpeg',
        getAsFile: () => unnamed,
      },
    ] as unknown as DataTransferItemList;

    const result = extractImageFileFromClipboardData(items, 'source');

    expect(result?.name).toBe('source.jpg');
    expect(result?.type).toBe('image/jpeg');
  });

  it('reports unsupported async clipboard access', async () => {
    setClipboard({});

    await expectAsync(readImageFileFromClipboard()).toBeRejectedWithError(
      'Clipboard image paste is not supported in this browser.',
    );
  });

  it('reads the first available image from the async clipboard', async () => {
    const image = new Blob(['pixels'], { type: 'image/png' });
    const getType = jasmine
      .createSpy('getType')
      .and.returnValue(Promise.resolve(image));
    setClipboard({
      read: jasmine.createSpy('read').and.returnValue(
        Promise.resolve([
          {
            types: ['text/plain'],
            getType,
          },
          {
            types: ['text/plain', 'image/png'],
            getType,
          },
        ]),
      ),
    });

    const result = await readImageFileFromClipboard('clipboard-source');

    expect(result.name).toBe('clipboard-source.png');
    expect(result.type).toBe('image/png');
    expect(getType).toHaveBeenCalledOnceWith('image/png');
  });

  it('rejects an async clipboard without an image', async () => {
    setClipboard({
      read: jasmine.createSpy('read').and.returnValue(
        Promise.resolve([
          {
            types: ['text/plain'],
            getType: jasmine.createSpy('getType'),
          },
        ]),
      ),
    });

    await expectAsync(readImageFileFromClipboard()).toBeRejectedWithError(
      'No image found in the clipboard.',
    );
  });
});
