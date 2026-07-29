import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CompendiumService } from '../../services/compendium.service';
import { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { TitleCasePipe } from '@angular/common';
import { RouterModule } from '@angular/router';
import { CreateCompendiumComponent } from '../../components/create-compendium/create-compendium.component';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { FormsModule } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import moment from 'moment';

@Component({
  selector: 'app-compendia',
  standalone: true,
  templateUrl: './compendia.component.html',
  styleUrl: './compendia.component.scss',
  imports: [TitleCasePipe, RouterModule, FormsModule, InputTextModule],
  providers: [DialogService],
})
export class CompendiaComponent implements OnInit, OnDestroy {
  compendia: CompendiumDto[] | null = null;
  filter = '';
  recordTypesToPreview: CompendiumRecordType[] = [
    CompendiumRecordType.Character,
    CompendiumRecordType.Place,
    CompendiumRecordType.Concept,
  ];
  private dialogService = inject(DialogService);
  private dialogRef: DynamicDialogRef | null = null;
  readonly compendiumService = inject(CompendiumService);

  get filteredCompendia(): CompendiumDto[] | null {
    if (this.compendia === null) {
      return null;
    }

    if (this.filter === '') {
      return this.compendia;
    }

    const lowerFilter = this.filter.toLowerCase();

    return this.compendia.filter((compendium) => {
      const nameMatch = compendium.name.toLowerCase().includes(lowerFilter);
      const descriptionMatch = compendium.description
        .toLowerCase()
        .includes(lowerFilter);
      const recordMatch = compendium.records.some((record) =>
        record.name.toLowerCase().includes(lowerFilter),
      );

      return nameMatch || descriptionMatch || recordMatch;
    });
  }

  ngOnInit(): void {
    this.getCompendia();
  }

  ngOnDestroy(): void {
    if (this.dialogRef) {
      this.dialogRef.close();
    }
  }

  getCompendia(): void {
    this.compendiumService.getCompendia().subscribe((compendia) => {
      this.compendia = [...compendia].sort((a, b) => {
        const aUpdatedAt = moment(a.updatedAt);
        const bUpdatedAt = moment(b.updatedAt);

        if (aUpdatedAt.isBefore(bUpdatedAt)) {
          return 1;
        } else if (aUpdatedAt.isAfter(bUpdatedAt)) {
          return -1;
        } else {
          return 0;
        }
      });
    });
  }

  getRecordsOfType(compendium: CompendiumDto, type: CompendiumRecordType) {
    const typeRecords = compendium.records.filter(
      (record) => record.type === type,
    );

    if (this.filter === '') {
      return typeRecords.slice(0, 3);
    }

    const filteredRecords = typeRecords.filter((record) =>
      record.name.toLowerCase().includes(this.filter.toLowerCase()),
    );

    return filteredRecords;
  }

  openCreateCompendiumDialog(): void {
    this.dialogRef = this.dialogService.open(CreateCompendiumComponent, {
      header: 'Create New Compendium',
      width: '50vw',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      modal: true,
      closable: true,
      closeOnEscape: true,
      dismissableMask: true,
    });

    this.dialogRef?.onClose.subscribe((result) => {
      if (result) {
        this.getCompendia();
      }
    });
  }
}
