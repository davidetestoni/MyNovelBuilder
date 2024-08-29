import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { NovelDto } from '../../types/dtos/novel/novel.dto';
import { NovelService } from '../../services/novel.service';
import { Prose } from '../../types/dtos/novel/prose';
import { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import { CompendiumService } from '../../services/compendium.service';
import { FormsModule } from '@angular/forms';
import { CompendiumRecordOverviewDto } from '../../types/dtos/compendium-record/compendium-record-overview.dto';
import { CompendiumRecordDto } from '../../types/dtos/compendium-record/compendium-record.dto';
import { SpacedPipe } from '../../pipes/spaced.pipe';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { CompendiumRecordImageDto } from '../../types/dtos/compendium-record/compendium-record-image.dto';
import { ProseEditorComponent } from '../../components/prose-editor/prose-editor.component';
import { PromptService } from '../../services/prompt.service';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';

@Component({
  selector: 'app-novel-editor',
  standalone: true,
  imports: [FormsModule, RouterModule, SpacedPipe, ProseEditorComponent],
  templateUrl: './novel-editor.component.html',
  styleUrl: './novel-editor.component.scss',
})
export class NovelEditorComponent {
  compendia: CompendiumDto[] | null = null;
  prompts: PromptDto[] | null = null; // TODO: Send a lighter version of this DTO
  novel: NovelDto | null = null;
  prose: Prose | null = null;
  readonly novelService: NovelService = inject(NovelService);
  readonly promptService: PromptService = inject(PromptService);
  readonly compendiumService: CompendiumService = inject(CompendiumService);
  novelId!: string;
  recordsFilter = '';
  selectedCompendium: CompendiumDto | null = null;
  selectedRecordOverview: CompendiumRecordOverviewDto | null = null;
  selectedRecord: CompendiumRecordDto | null = null;
  floatedImages: CompendiumRecordImageDto[] = [];
  lastHoveredFloatingImageId: string | null = null;
  zoomedImage: CompendiumRecordImageDto | null = null;

  compendiumRecordTypes: CompendiumRecordType[] = [
    CompendiumRecordType.Character,
    CompendiumRecordType.Place,
    CompendiumRecordType.Object,
    CompendiumRecordType.Event,
    CompendiumRecordType.Concept,
    CompendiumRecordType.Other,
  ];

  CompendiumRecordType = CompendiumRecordType;

  constructor(private route: ActivatedRoute) {}

  ngOnInit(): void {
    this.novelId = this.route.snapshot.paramMap.get('id')!;
    this.getNovel();
    this.getProse();
    this.getPrompts();
    this.restoreFloatedImages();
  }

  getNovel(): void {
    this.novelService.getNovel(this.novelId).subscribe((novel) => {
      this.novel = novel;
      this.getCompendia();
    });
  }

  getProse(): void {
    this.novelService.getNovelProse(this.novelId).subscribe((prose) => {
      this.prose = prose;
    });
  }

  getPrompts(): void {
    this.promptService.getPrompts().subscribe((prompts) => {
      this.prompts = prompts;
    });
  }

  getCompendia(): void {
    // TODO: Only get the novel's compendia, not all compendia
    this.compendiumService.getCompendia().subscribe((compendia) => {
      this.compendia = compendia.filter((compendium) =>
        this.novel?.compendiumIds.includes(compendium.id)
      );
    });
  }

  restoreFloatedImages(): void {
    this.floatedImages = this.novelService.getFloatedImagesForNovel(
      this.novelId
    );
  }

  getCompendiumRecordsByType(
    type: CompendiumRecordType
  ): CompendiumRecordOverviewDto[] {
    if (this.compendia === null) {
      return [];
    }

    if (this.selectedCompendium === null) {
      const records = this.compendia
        .map((compendium) => compendium.records)
        .flat()
        .filter(
          (record) =>
            record.type === type &&
            record.name.toLowerCase().includes(this.recordsFilter.toLowerCase())
        );

      records.sort((a, b) => a.name.localeCompare(b.name));

      return records;
    }

    return this.selectedCompendium.records.filter(
      (record) =>
        record.type === type &&
        record.name.toLowerCase().includes(this.recordsFilter.toLowerCase())
    );
  }

  selectRecord(record: CompendiumRecordOverviewDto): void {
    // If the record is already selected, deselect it
    if (this.selectedRecordOverview === record) {
      this.selectedRecordOverview = null;
      this.selectedRecord = null;
      return;
    }

    this.selectedRecordOverview = record;
    this.compendiumService.getRecord(record.id).subscribe((record) => {
      this.selectedRecord = record;
    });
  }

  isFloatedImage(image: CompendiumRecordImageDto): boolean {
    return this.floatedImages.some(
      (floatedImage) => floatedImage.id === image.id
    );
  }

  floatImage(image: CompendiumRecordImageDto): void {
    // If the image is already floated, unfloat it (use id instead of object reference)
    if (this.isFloatedImage(image)) {
      this.floatedImages = this.floatedImages.filter(
        (floatedImage) => floatedImage.id !== image.id
      );
      return;
    }

    this.floatedImages = [...this.floatedImages, image];
    this.novelService.setFloatedImagesForNovel(
      this.novelId,
      this.floatedImages
    );
  }

  zoomImage(image: CompendiumRecordImageDto): void {
    this.zoomedImage = image;
  }

  unzoomImage(): void {
    this.zoomedImage = null;
  }

  updateProse(prose: Prose) {
    this.novelService.updateNovelProse(this.novelId, prose).subscribe();
  }
}
