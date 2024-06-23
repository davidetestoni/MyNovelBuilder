import { Component, OnInit, inject } from '@angular/core';
import { CompendiumService } from '../../services/compendium.service';
import { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { TitleCasePipe } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { CreateCompendiumComponent } from '../../components/create-compendium/create-compendium.component';

@Component({
  selector: 'app-compendia',
  standalone: true,
  imports: [TitleCasePipe, RouterModule],
  templateUrl: './compendia.component.html',
  styleUrl: './compendia.component.scss',
})
export class CompendiaComponent implements OnInit {
  compendia: CompendiumDto[] | null = null;
  recordTypesToPreview: CompendiumRecordType[] = [
    CompendiumRecordType.Character,
    CompendiumRecordType.Place,
    CompendiumRecordType.Concept,
  ];
  readonly dialog = inject(MatDialog);
  readonly compendiumService = inject(CompendiumService);

  ngOnInit(): void {
    this.getCompendia();
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
    const dialogRef = this.dialog.open(CreateCompendiumComponent, {
      minWidth: '50vw',
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.getCompendia();
      }
    });
  }
}
