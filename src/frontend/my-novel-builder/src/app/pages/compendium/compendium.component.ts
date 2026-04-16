import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import { CompendiumService } from '../../services/compendium.service';
import { FormsModule } from '@angular/forms';
import { CompendiumRecordDto } from '../../types/dtos/compendium-record/compendium-record.dto';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { TitleCasePipe } from '@angular/common';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { CreateCompendiumRecordComponent } from '../../components/create-compendium-record/create-compendium-record.component';
import { CompendiumRecordComponent } from '../../components/edit-compendium-record/compendium-record.component';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { ButtonModule } from 'primeng/button';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';

@Component({
  selector: 'app-compendium',
  standalone: true,
  imports: [
    FormsModule,
    TitleCasePipe,
    CompendiumRecordComponent,
    InputTextModule,
    TextareaModule,
    ButtonModule,
    ConfirmDialogModule,
  ],
  providers: [DialogService, ConfirmationService],
  templateUrl: './compendium.component.html',
  styleUrl: './compendium.component.scss',
})
export class CompendiumComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private dialogService = inject(DialogService);
  private confirmationService = inject(ConfirmationService);

  compendium: CompendiumDto | null = null;
  records: CompendiumRecordDto[] | null = null;
  filter = '';
  readonly compendiumService: CompendiumService = inject(CompendiumService);
  compendiumId!: string;
  currentRecord: CompendiumRecordDto | null = null;
  private dialogRef: DynamicDialogRef | null = null;

  compendiumRecordTypes: CompendiumRecordType[] = [
    CompendiumRecordType.Character,
    CompendiumRecordType.Place,
    CompendiumRecordType.Object,
    CompendiumRecordType.Event,
    CompendiumRecordType.Concept,
    CompendiumRecordType.Other,
  ];

  ngOnInit(): void {
    this.compendiumId = this.route.snapshot.paramMap.get('id')!;
    this.getCompendium();

    this.route.paramMap.subscribe((params) => {
      const recordId = params.get('recordId');
      if (this.records && recordId) {
        this.currentRecord =
          this.records.find((record) => record.id === recordId) ?? null;
      }
    });

    this.getRecords();
  }

  ngOnDestroy(): void {
    if (this.dialogRef) {
      this.dialogRef.close();
    }
  }

  getCompendium(): void {
    this.compendiumService
      .getCompendium(this.compendiumId)
      .subscribe((compendium) => {
        this.compendium = compendium;
      });
  }

  getRecords(): void {
    const recordId = this.route.snapshot.paramMap.get('recordId');

    this.compendiumService
      .getRecords(this.compendiumId)
      .subscribe((records) => {
        this.records = records;

        // If there was a selected record, update it with the latest data
        if (this.currentRecord) {
          this.currentRecord =
            this.records.find(
              (record) => record.id === this.currentRecord!.id,
            ) ?? null;
        } else if (recordId) {
          this.currentRecord =
            this.records.find((record) => record.id === recordId) ?? null;
        }
      });
  }

  setCurrentRecord(record: CompendiumRecordDto): void {
    this.currentRecord = record;
    this.router.navigate([
      '/compendium',
      this.compendiumId,
      'record',
      record.id,
    ]);
  }

  getRecordsOfType(type: CompendiumRecordType) {
    if (this.records === null) {
      return [];
    }

    const typeRecords = this.records.filter((record) => record.type === type);

    if (this.filter === '') {
      return typeRecords;
    }

    const lowerFilter = this.filter.toLowerCase();

    return typeRecords.filter(
      (record) =>
        record.name.toLowerCase().includes(lowerFilter) ||
        record.aliases.toLowerCase().includes(lowerFilter),
    );
  }

  getRecordImage(record: CompendiumRecordDto): string | null {
    const mainImage = record.media.filter((image) => image.isCurrent);
    return mainImage.length > 0 ? mainImage[0].url : null;
  }

  openCreateRecordDialog(): void {
    this.dialogRef = this.dialogService.open(CreateCompendiumRecordComponent, {
      header: 'Create Record',
      width: '50vw',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      closable: true,
      closeOnEscape: true,
      modal: true,
      dismissableMask: true,
      data: { compendiumId: this.compendiumId },
    });

    this.dialogRef?.onClose.subscribe((record: CompendiumRecordDto) => {
      if (record) {
        // Select the newly created record, then refresh the records
        // (this will also update the current record)
        this.setCurrentRecord(record);

        this.getRecords();
      }
    });
  }

  updateCompendium(): void {
    if (this.compendium) {
      this.compendiumService
        .updateCompendium({
          id: this.compendium.id,
          name: this.compendium.name,
          description: this.compendium.description,
        })
        .subscribe((compendium) => {
          this.compendium = compendium;
        });
    }
  }

  updateRecord(record: CompendiumRecordDto): void {
    this.compendiumService
      .updateRecord({
        id: record.id,
        name: record.name,
        aliases: record.aliases,
        type: record.type,
        context: record.context,
        alwaysIncluded: record.alwaysIncluded,
        characterVoiceAssignments: record.characterVoiceAssignments,
      })
      .subscribe((updatedRecord) => {
        if (this.currentRecord?.id === updatedRecord.id) {
          this.currentRecord = updatedRecord;
        }

        if (this.records) {
          const idx = this.records.findIndex((r) => r.id === updatedRecord.id);
          if (idx >= 0) {
            this.records[idx] = updatedRecord;
          }
        }
      });
  }

  deleteRecord(record: CompendiumRecordDto): void {
    this.compendiumService.deleteRecord(record.id).subscribe(() => {
      this.getRecords();
      this.currentRecord = null;
      this.router.navigate(['/compendium', this.compendiumId]);
    });
  }

  deleteCompendium(): void {
    if (this.compendium === null) {
      return;
    }

    this.confirmationService.confirm({
      message:
        'Are you sure you want to remove this compendium and all of its records? This action cannot be undone.',
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.compendiumService
          .deleteCompendium(this.compendium!.id)
          .subscribe(() => {
            // Redirect to the compendia page
            window.location.href = '/compendia';
          });
      },
    });
  }
}
