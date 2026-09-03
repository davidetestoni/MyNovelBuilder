import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DynamicDialogRef } from 'primeng/dynamicdialog';
import { MultiSelect } from 'primeng/multiselect';
import { Select } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { CompendiumService } from '../../services/compendium.service';
import { NovelService } from '../../services/novel.service';
import { WorldBuildingSessionService } from '../../services/world-building-session.service';
import { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import { NovelDto } from '../../types/dtos/novel/novel.dto';

@Component({
  selector: 'app-create-world-building-session',
  standalone: true,
  templateUrl: './create-world-building-session.component.html',
  styleUrls: ['./create-world-building-session.component.scss'],
  imports: [FormsModule, MultiSelect, Select, TextareaModule],
})
export class CreateWorldBuildingSessionComponent implements OnInit {
  private dialogRef = inject(DynamicDialogRef);
  private novelService = inject(NovelService);
  private compendiumService = inject(CompendiumService);
  private worldBuildingSessionService = inject(WorldBuildingSessionService);

  novels: NovelDto[] = [];
  compendia: CompendiumDto[] = [];

  name: string | null = null;
  novelId: string | null = null;
  compendiumIds: string[] = [];
  freeformPremise: string | null = null;
  isCreating = false;

  ngOnInit(): void {
    this.novelService.getNovels().subscribe((novels) => {
      this.novels = novels;
    });
    this.compendiumService.getCompendia().subscribe((compendia) => {
      this.compendia = compendia;
    });
  }

  createSession(): void {
    if (this.isCreating) {
      return;
    }

    this.isCreating = true;
    this.worldBuildingSessionService
      .createSession({
        name: this.name?.trim() || null,
        novelId: this.novelId,
        chapterIndex: null,
        compendiumIds: this.compendiumIds,
        compendiumRecordIds: [],
        freeformPremise: this.freeformPremise?.trim() || null,
      })
      .subscribe({
        next: (session) => {
          this.dialogRef.close(session);
        },
        error: () => {
          this.isCreating = false;
        },
      });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
