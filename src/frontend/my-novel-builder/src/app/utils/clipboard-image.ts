function extensionFromMimeType(mimeType: string): string {
  const [, subtype = 'png'] = mimeType.split('/');
  return subtype.split('+')[0] || 'png';
}

export async function readImageFileFromClipboard(): Promise<File> {
  if (!navigator.clipboard?.read) {
    throw new Error('Clipboard image reading is not supported in this browser.');
  }

  const clipboardItems = await navigator.clipboard.read();

  for (const item of clipboardItems) {
    const imageType = item.types.find((type) => type.startsWith('image/'));
    if (!imageType) {
      continue;
    }

    const blob = await item.getType(imageType);
    const extension = extensionFromMimeType(blob.type || imageType);

    return new File([blob], `clipboard-image.${extension}`, {
      type: blob.type || imageType,
      lastModified: Date.now(),
    });
  }

  throw new Error('No image found in clipboard.');
}
