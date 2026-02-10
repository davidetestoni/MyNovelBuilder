import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { CompendiumService } from '../../services/compendium.service';
import { FormsModule } from '@angular/forms';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { TitleCasePipe } from '@angular/common';
import { CompendiumRecordDto } from '../../types/dtos/compendium-record/compendium-record.dto';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { GenerateImageComponent } from '../generate-image/generate-image.component';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { AliasSuggestionsComponent } from '../alias-suggestions/alias-suggestions.component';

@Component({
  selector: 'app-compendium-record',
  standalone: true,
  imports: [
    FormsModule,
    TitleCasePipe,
    InputTextModule,
    TextareaModule,
    ButtonModule,
    CheckboxModule,
    ConfirmDialogModule,
    AliasSuggestionsComponent,
  ],
  providers: [DialogService, ConfirmationService],
  templateUrl: './compendium-record.component.html',
  styleUrl: './compendium-record.component.scss',
})
export class CompendiumRecordComponent {
  @Input() record!: CompendiumRecordDto;
  @Output() updateRecord = new EventEmitter<CompendiumRecordDto>();
  @Output() deleteRecord = new EventEmitter<CompendiumRecordDto>();
  readonly compendiumService: CompendiumService = inject(CompendiumService);
  private dialogService = inject(DialogService);
  private confirmationService = inject(ConfirmationService);
  private dialogRef: DynamicDialogRef | null = null;

  recordTypes: CompendiumRecordType[] = [
    CompendiumRecordType.Character,
    CompendiumRecordType.Place,
    CompendiumRecordType.Object,
    CompendiumRecordType.Event,
    CompendiumRecordType.Concept,
    CompendiumRecordType.Other,
  ];

  CompendiumRecordType = CompendiumRecordType;

  addAlias(alias: string): void {
    const currentAliasesValue = this.record.aliases || '';
    const currentAliases = currentAliasesValue
      .split(',')
      .map((a) => a.trim())
      .filter((a) => a.length > 0);

    if (!currentAliases.some((a) => a.toLowerCase() === alias.toLowerCase())) {
      currentAliases.push(alias);
      this.record.aliases = currentAliases.join(', ');
      this.updateRecord.emit(this.record);
    }
  }

  ngOnDestroy(): void {
    if (this.dialogRef) {
      this.dialogRef.close();
    }
  }

  onBlur(): void {
    this.updateRecord.emit(this.record);
  }

  setCurrentImage(imageId: string): void {
    this.record.media.forEach((image) => {
      image.isCurrent = image.id === imageId;
    });
    this.compendiumService
      .setCurrentRecordImage(this.record.id, imageId)
      .subscribe();
  }

  removeMedia(mediaId: string): void {
    this.record.media = this.record.media.filter(
      (media) => media.id !== mediaId,
    );
    this.compendiumService
      .deleteRecordMedia(this.record.id, mediaId)
      .subscribe();
  }

  removeRecord(): void {
    this.confirmationService.confirm({
      message:
        'Are you sure you want to delete this record? This action cannot be undone.',
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.deleteRecord.emit(this.record);
      },
    });
  }

  addMedia(): void {
    const fileInput = document.createElement('input');
    fileInput.type = 'file';
    fileInput.accept = 'image/*,video/*';
    fileInput.onchange = () => {
      if (fileInput.files && fileInput.files.length > 0) {
        const file = fileInput.files[0];
        this.compendiumService
          .uploadRecordMedia(
            this.record.id,
            file,
            this.record.media.length === 0,
          )
          .subscribe(() => {
            // Get the record and update the media
            this.compendiumService
              .getRecord(this.record.id)
              .subscribe((record) => {
                this.record.media = record.media;
                this.updateRecord.emit(this.record);
              });

            fileInput.remove();
          });
      }
    };
    fileInput.click();
  }

  generateImage() {
    this.dialogRef = this.dialogService.open(GenerateImageComponent, {
      header: 'Generate Image',
      width: '50vw',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      closable: true,
      closeOnEscape: true,
      modal: true,
      dismissableMask: true,
    });

    this.dialogRef?.onClose.subscribe((image: Blob) => {
      if (image) {
        this.compendiumService
          .uploadRecordMedia(
            this.record.id,
            image,
            this.record.media.length === 0,
          )
          .subscribe(() => {
            // Get the record and update the media
            this.compendiumService
              .getRecord(this.record.id)
              .subscribe((record) => {
                this.record.media = record.media;
                this.updateRecord.emit(this.record);
              });
          });
      }
    });
  }
}
