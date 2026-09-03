import { Component, EventEmitter, Input, Output } from '@angular/core';
import { ChipModule } from 'primeng/chip';

@Component({
  selector: 'app-alias-suggestions',
  standalone: true,
  imports: [ChipModule],
  templateUrl: './alias-suggestions.component.html',
  styleUrl: './alias-suggestions.component.scss',
})
export class AliasSuggestionsComponent {
  @Input() name = '';
  @Input() currentAliases = '';
  @Output() aliasAdded = new EventEmitter<string>();

  get suggestedAliases(): string[] {
    const name = (this.name || '').trim();
    const aliases = this.currentAliases || '';

    if (!name.includes(' ')) {
      return [];
    }

    const currentAliasesList = aliases
      .split(',')
      .map((a) => a.trim().toLowerCase())
      .filter((a) => a.length > 0);

    const parts = name
      .split(/\s+/)
      .map((part) => part.trim())
      .filter((part) => part.length > 1);

    return [...new Set(parts)].filter(
      (part) =>
        !currentAliasesList.includes(part.toLowerCase()) &&
        part.toLowerCase() !== name.toLowerCase(),
    );
  }

  addAlias(alias: string): void {
    this.aliasAdded.emit(alias);
  }
}
