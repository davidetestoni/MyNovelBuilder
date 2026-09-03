import { CompendiumDto } from '../types/dtos/compendium/compendium.dto';

export function getCompendiumPreviewImages(
  compendium: CompendiumDto,
  count = 3,
): Array<string | null> {
  // Record overview items do not expose timestamps, so the API order is treated
  // as chronological and we only bias toward entries that actually have images.
  const prioritizedRecords = [...compendium.records].sort((a, b) => {
    if (!!a.imageUrl === !!b.imageUrl) {
      return 0;
    }

    return a.imageUrl ? -1 : 1;
  });

  const previewImages = prioritizedRecords
    .slice(0, count)
    .map((record) => record.imageUrl);

  while (previewImages.length < count) {
    previewImages.push(null);
  }

  return previewImages;
}
