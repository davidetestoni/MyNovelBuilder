import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { CompendiumService } from '../../services/compendium.service';
import { FormsModule } from '@angular/forms';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { TitleCasePipe } from '@angular/common';
import { CompendiumRecordDto } from '../../types/dtos/compendium-record/compendium-record.dto';
import { MatDialog } from '@angular/material/dialog';
import { GenerateImageComponent } from '../generate-image/generate-image.component';

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
  readonly compendiumService: CompendiumService = inject(CompendiumService);
  readonly dialog = inject(MatDialog);

  recordTypes: CompendiumRecordType[] = [
    CompendiumRecordType.Character,
    CompendiumRecordType.Place,
    CompendiumRecordType.Object,
    CompendiumRecordType.Event,
    CompendiumRecordType.Concept,
    CompendiumRecordType.Other,
  ];

  CompendiumRecordType = CompendiumRecordType;

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

  removeRecord(): void {
    if (!confirm('Are you sure you want to delete this record? This action cannot be undone.')) {
      return;
    }

    this.deleteRecord.emit(this.record);
  }

  addImage(): void {
    const fileInput = document.createElement('input');
    fileInput.type = 'file';
    fileInput.accept = 'image/*';
    fileInput.onchange = () => {
      if (fileInput.files && fileInput.files.length > 0) {
        const file = fileInput.files[0];
        this.compendiumService
          .uploadRecordImage(
            this.record.id,
            file,
            this.record.images.length === 0
          )
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

  generateImage() {
    this.dialog.open(GenerateImageComponent, {
      minWidth: '50vw',
    }).afterClosed().subscribe((image: Blob) => {
      if (image) {
        this.compendiumService
          .uploadRecordImage(
            this.record.id,
            image,
            this.record.images.length === 0
          ).subscribe(() => {
            // Get the record and update the images
            this.compendiumService
            .getRecord(this.record.id)
            .subscribe((record) => {
              this.record.images = record.images;
              this.updateRecord.emit(this.record);
            });
        });
      }
    });
  }
}
