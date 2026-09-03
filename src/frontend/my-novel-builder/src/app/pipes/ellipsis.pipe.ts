import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'ellipsis',
  standalone: true,
})
export class EllipsisPipe implements PipeTransform {
  transform(
    value: string | null | undefined,
    length: number,
  ): string {
    if (!value) {
      return '';
    }

    if (!Number.isFinite(length)) {
      return value;
    }

    const normalizedLength = Math.max(0, Math.floor(length));
    return value.length > normalizedLength
      ? `${value.slice(0, normalizedLength)}...`
      : value;
  }
}
