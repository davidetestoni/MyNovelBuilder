import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

@Component({
  selector: 'app-record-option-preview',
  standalone: true,
  templateUrl: './record-option-preview.component.html',
  styleUrl: './record-option-preview.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RecordOptionPreviewComponent {
  @Input({ required: true }) name!: string;
  @Input() imageUrl: string | null = null;
}
