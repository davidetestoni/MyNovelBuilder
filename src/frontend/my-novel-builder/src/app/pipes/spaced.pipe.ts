import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'spaced',
  standalone: true,
})
export class SpacedPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    if (!value) {
      return '';
    }

    const replaced = value
      .replace(/([A-Z]+)([A-Z][a-z])/g, '$1 $2')
      .replace(/([a-z\d])([A-Z])/g, '$1 $2');
    return replaced.charAt(0).toUpperCase() + replaced.slice(1);
  }
}
