import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'spaced',
  standalone: true,
})
export class SpacedPipe implements PipeTransform {
  transform(value: string): string {
    if (!value || value.length === 0) {
      return '';
    }

    // This pipe will turn camelCase into Camel Case
    let replaced = value.replace(/([a-z])([A-Z])/g, '$1 $2');
    return replaced.charAt(0).toUpperCase() + replaced.slice(1);
  }
}
