import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'fileSize',
  standalone: true,
})
export class FileSizePipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    if (
      value === null ||
      value === undefined ||
      !Number.isFinite(value) ||
      value < 0
    ) {
      return '';
    }

    if (value < 1024) {
      return `${Math.round(value)} B`;
    }

    const units = ['KB', 'MB', 'GB', 'TB'];
    let size = value;
    let unitIndex = -1;

    do {
      size /= 1024;
      unitIndex += 1;
    } while (size >= 1024 && unitIndex < units.length - 1);

    const fractionDigits = size >= 10 ? 0 : 1;
    return `${size.toFixed(fractionDigits)} ${units[unitIndex]}`;
  }
}
