import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'ellipsis',
  standalone: true,
})
export class EllipsisPipe implements PipeTransform {
  transform(value: string, length: number): string {
    return value.length > length ? value.slice(0, length) + '...' : value;
  }
}
