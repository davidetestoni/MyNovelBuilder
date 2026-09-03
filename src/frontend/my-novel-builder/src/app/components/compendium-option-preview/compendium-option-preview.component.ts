import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import { getCompendiumPreviewImages } from '../../utils/compendium-preview';

@Component({
  selector: 'app-compendium-option-preview',
  standalone: true,
  templateUrl: './compendium-option-preview.component.html',
  styleUrl: './compendium-option-preview.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CompendiumOptionPreviewComponent {
  @Input({ required: true }) compendium!: CompendiumDto;

  get previewImages(): Array<string | null> {
    return getCompendiumPreviewImages(this.compendium);
  }
}
