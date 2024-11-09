import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import { CompendiumService } from '../../services/compendium.service';
import { FormsModule } from '@angular/forms';
import { CompendiumRecordDto } from '../../types/dtos/compendium-record/compendium-record.dto';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { TitleCasePipe } from '@angular/common';
import { MatDialog } from '@angular/material/dialog';
import { CreateCompendiumRecordComponent } from '../../components/create-compendium-record/create-compendium-record.component';
import { CompendiumRecordComponent } from '../../components/edit-compendium-record/compendium-record.component';

@Component({
  selector: 'app-compendium',
  standalone: true,
  imports: [FormsModule, TitleCasePipe, CompendiumRecordComponent],
  templateUrl: './compendium.component.html',
  styleUrl: './compendium.component.scss',
})
export class CompendiumComponent implements OnInit {
  compendium: CompendiumDto | null = null;
  records: CompendiumRecordDto[] | null = null;
  readonly dialog = inject(MatDialog);
  readonly compendiumService: CompendiumService = inject(CompendiumService);
  compendiumId!: string;
  currentRecord: CompendiumRecordDto | null = null;

  compendiumRecordTypes: CompendiumRecordType[] = [
    CompendiumRecordType.Character,
    CompendiumRecordType.Place,
    CompendiumRecordType.Object,
    CompendiumRecordType.Event,
    CompendiumRecordType.Concept,
    CompendiumRecordType.Other,
  ];

  constructor(private route: ActivatedRoute) {}

  ngOnInit(): void {
    this.compendiumId = this.route.snapshot.paramMap.get('id')!;
    this.getCompendium();
    this.getRecords();
  }

  getCompendium(): void {
    this.compendiumService
      .getCompendium(this.compendiumId)
      .subscribe((compendium) => {
        this.compendium = compendium;
      });
  }

  getRecords(): void {
    this.compendiumService
      .getRecords(this.compendiumId)
      .subscribe((records) => {
        this.records = records;

        // If there was a selected record, update it with the latest data
        if (this.currentRecord) {
          this.currentRecord =
            this.records.find(
              (record) => record.id === this.currentRecord!.id
            ) ?? null;
        }
      });
  }

  setCurrentRecord(record: CompendiumRecordDto): void {
    this.currentRecord = record;
  }

  getRecordsOfType(type: CompendiumRecordType) {
    return this.records!.filter((record) => record.type === type);
  }

  getRecordImage(record: CompendiumRecordDto): string | null {
    const mainImage = record.images.filter((image) => image.isCurrent);
    return mainImage.length > 0 ? mainImage[0].url : null;
  }

  openCreateRecordDialog(): void {
    const dialogRef = this.dialog.open(CreateCompendiumRecordComponent, {
      minWidth: '50vw',
      data: { compendiumId: this.compendiumId },
    });

    dialogRef.afterClosed().subscribe((record: CompendiumRecordDto) => {
      if (record) {
        // Select the newly created record, then refresh the records
        // (this will also update the current record)
        this.currentRecord = record;

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
      })
      .subscribe();
  }

  deleteRecord(record: CompendiumRecordDto): void {
    this.compendiumService.deleteRecord(record.id).subscribe(() => {
      this.getRecords();
      this.currentRecord = null;
    });
  }

  deleteCompendium(): void {
    if (this.compendium === null) {
      return;
    }

    if (
      !confirm(
        'Are you sure you want to remove this compendium and all of its records? This action cannot be undone.'
      )
    ) {
      return;
    }

    this.compendiumService.deleteCompendium(this.compendium.id).subscribe(() => {
      // Redirect to the compendia page
      window.location.href = '/compendia';
    });
  }
}
