import { extensionForGeneratedMediaMimeType } from './generated-media';

function createClipboardImageFile(blob: Blob, baseName: string): File {
  const mimeType = blob.type || 'image/png';
  const extension = extensionForGeneratedMediaMimeType(mimeType);

  return new File([blob], `${baseName}.${extension}`, {
    type: mimeType,
    lastModified: Date.now(),
  });
}

export function extractImageFileFromClipboardData(
  items: DataTransferItemList | null | undefined,
  baseName = 'clipboard-image',
): File | null {
  if (items === null || items === undefined) {
    return null;
  }

  for (const item of Array.from(items)) {
    if (item.kind !== 'file' || !item.type.startsWith('image/')) {
      continue;
    }

    const file = item.getAsFile();
    if (file === null) {
      continue;
    }

    return file.name
      ? file
      : createClipboardImageFile(file, baseName);
  }

  return null;
}

export async function readImageFileFromClipboard(
  baseName = 'clipboard-image',
): Promise<File> {
  if (
    typeof navigator === 'undefined' ||
    navigator.clipboard === undefined ||
    typeof navigator.clipboard.read !== 'function'
  ) {
    throw new Error('Clipboard image paste is not supported in this browser.');
  }

  const clipboardItems = await navigator.clipboard.read();

  for (const item of clipboardItems) {
    const imageMimeType = item.types.find((type) => type.startsWith('image/'));
    if (imageMimeType === undefined) {
      continue;
    }

    const blob = await item.getType(imageMimeType);
    return createClipboardImageFile(blob, baseName);
  }

  throw new Error('No image found in the clipboard.');
}
