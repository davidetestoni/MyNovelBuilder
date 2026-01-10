import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CompendiumService } from '../../services/compendium.service';
import { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { TitleCasePipe } from '@angular/common';
import { RouterModule } from '@angular/router';
import { CreateCompendiumComponent } from '../../components/create-compendium/create-compendium.component';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';

@Component({
  selector: 'app-compendia',
  standalone: true,
  templateUrl: './compendia.component.html',
  styleUrl: './compendia.component.scss',
  imports: [TitleCasePipe, RouterModule],
  providers: [DialogService],
})
export class CompendiaComponent implements OnInit, OnDestroy {
  compendia: CompendiumDto[] | null = null;
  recordTypesToPreview: CompendiumRecordType[] = [
    CompendiumRecordType.Character,
    CompendiumRecordType.Place,
    CompendiumRecordType.Concept,
  ];
  private dialogService = inject(DialogService);
  private dialogRef: DynamicDialogRef | null = null;
  readonly compendiumService = inject(CompendiumService);

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
      this.compendia = compendia;
    });
  }

  getRecordsOfType(compendium: CompendiumDto, type: CompendiumRecordType) {
    return compendium.records
      .filter((record) => record.type === type)
      .slice(0, 5);
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
