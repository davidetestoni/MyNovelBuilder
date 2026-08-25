import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

@Component({
  selector: 'app-option-preview',
  standalone: true,
  templateUrl: './option-preview.component.html',
  styleUrl: './option-preview.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OptionPreviewComponent {
  @Input({ required: true }) label!: string;
  @Input() imageUrl: string | null = null;
  @Input() fallbackIcon = 'icon-compendium';
}
