import {
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  inject,
} from '@angular/core';
import { CompendiumService } from '../../services/compendium.service';
import { FormsModule } from '@angular/forms';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { TitleCasePipe } from '@angular/common';
import { CompendiumRecordDto } from '../../types/dtos/compendium-record/compendium-record.dto';

@Component({
  selector: 'app-compendium-record',
  standalone: true,
  imports: [FormsModule, TitleCasePipe],
  templateUrl: './compendium-record.component.html',
  styleUrl: './compendium-record.component.scss',
})
export class CompendiumRecordComponent {
  @Input() record!: CompendiumRecordDto;
  @Output() updateRecord = new EventEmitter<CompendiumRecordDto>();
  @Output() deleteRecord = new EventEmitter<CompendiumRecordDto>();
  compendiumService: CompendiumService = inject(CompendiumService);

  recordTypes: CompendiumRecordType[] = [
    CompendiumRecordType.Character,
    CompendiumRecordType.Place,
    CompendiumRecordType.Object,
    CompendiumRecordType.Event,
    CompendiumRecordType.Concept,
    CompendiumRecordType.Other,
  ];

  onBlur(): void {
    this.updateRecord.emit(this.record);
  }

  setCurrentImage(imageId: string): void {
    this.record.images.forEach((image) => {
      image.isCurrent = image.id === imageId;
    });
    this.compendiumService
      .setCurrentRecordImage(this.record.id, imageId)
      .subscribe();
  }

  removeImage(imageId: string): void {
    this.record.images = this.record.images.filter(
      (image) => image.id !== imageId
    );
    this.compendiumService
      .deleteRecordImage(this.record.id, imageId)
      .subscribe();
  }

  addImage(): void {
    const fileInput = document.createElement('input');
    fileInput.type = 'file';
    fileInput.accept = 'image/png';
    fileInput.onchange = () => {
      if (fileInput.files && fileInput.files.length > 0) {
        const file = fileInput.files[0];
        this.compendiumService
          .uploadRecordImage(this.record.id, file, false)
          .subscribe(() => {
            // Get the record and update the images
            this.compendiumService
              .getRecord(this.record.id)
              .subscribe((record) => {
                this.record.images = record.images;
                this.updateRecord.emit(this.record);
              });

            fileInput.remove();
          });
      }
    };
    fileInput.click();
  }
}
