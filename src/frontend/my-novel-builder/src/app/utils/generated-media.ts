export function extensionForGeneratedMediaMimeType(mimeType: string): string {
  switch (mimeType) {
    case 'video/mp4':
      return 'mp4';
    case 'image/jpeg':
      return 'jpg';
    case 'image/webp':
      return 'webp';
    case 'image/gif':
      return 'gif';
    default:
      return 'png';
  }
}

export function createGeneratedMediaFile(
  blob: Blob,
  baseName = 'generated-media',
): File {
  const mimeType = blob.type || 'image/png';
  const extension = extensionForGeneratedMediaMimeType(mimeType);

  return new File([blob], `${baseName}.${extension}`, {
    type: mimeType,
    lastModified: Date.now(),
  });
}

export function isVideoMimeType(mimeType: string | null | undefined): boolean {
  return mimeType?.startsWith('video/') ?? false;
}
