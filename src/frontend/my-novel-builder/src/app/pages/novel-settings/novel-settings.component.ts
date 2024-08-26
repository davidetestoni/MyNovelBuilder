import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { NovelDto } from '../../types/dtos/novel/novel.dto';
import { NovelService } from '../../services/novel.service';
import { FormsModule } from '@angular/forms';
import { WritingTense } from '../../types/enums/writing-tense';
import { WritingPov } from '../../types/enums/writing-pov';
import { WritingLanguage } from '../../types/enums/writing-language';
import { SpacedPipe } from '../../pipes/spaced.pipe';
import { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import { CompendiumService } from '../../services/compendium.service';
import { CompendiumRecordOverviewDto } from '../../types/dtos/compendium-record/compendium-record-overview.dto';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';

@Component({
  selector: 'app-novel-settings',
  standalone: true,
  imports: [FormsModule, RouterModule, SpacedPipe],
  templateUrl: './novel-settings.component.html',
  styleUrl: './novel-settings.component.scss',
})
export class NovelSettingsComponent {
  novel: NovelDto | null = null;
  compendia: CompendiumDto[] | null = null;
  readonly novelService: NovelService = inject(NovelService);
  readonly compendiumService: CompendiumService = inject(CompendiumService);
  novelId!: string;

  writingTenses: WritingTense[] = [WritingTense.Past, WritingTense.Present];

  writingPovs: WritingPov[] = [
    WritingPov.FirstPerson,
    WritingPov.ThirdPersonLimited,
    WritingPov.ThirdPersonOmniscient,
  ];

  writingLanguages: WritingLanguage[] = [
    WritingLanguage.English,
    WritingLanguage.Italian,
    WritingLanguage.French,
    WritingLanguage.Spanish,
    WritingLanguage.German,
    WritingLanguage.Russian,
  ];

  constructor(private route: ActivatedRoute) {}

  ngOnInit(): void {
    this.novelId = this.route.snapshot.paramMap.get('id')!;
    this.getNovel();
    this.getCompendia();
  }

  getNovel(): void {
    this.novelService.getNovel(this.novelId).subscribe((novel) => {
      this.novel = novel;
    });
  }

  getCompendia(): void {
    this.compendiumService.getCompendia().subscribe((compendia) => {
      this.compendia = compendia;
    });
  }

  onBlur() {
    if (this.novel === null) {
      return;
    }

    console.log('Updating novel', this.novel);

    this.novelService
      .updateNovel({
        id: this.novel.id,
        title: this.novel.title,
        author: this.novel.author,
        brief: this.novel.brief,
        tense: this.novel.tense,
        pov: this.novel.pov,
        language: this.novel.language,
        mainCharacterId: this.novel.mainCharacterId,
        compendiumIds: this.novel.compendiumIds,
      })
      .subscribe();
  }

  getAvailableCharacters(): CompendiumRecordOverviewDto[] {
    if (this.novel === null || this.compendia === null) {
      return [];
    }

    // Get all the compendiums that are selected in the novel
    // and then get all the records from those compendiums that
    // are of type 'character'
    return this.compendia
      .filter((compendium) => this.novel?.compendiumIds.includes(compendium.id))
      .map((compendium) => compendium.records)
      .flat()
      .filter((record) => record.type === CompendiumRecordType.Character);
  }

  toggleCompendium(compendiumId: string): void {
    if (this.novel === null) {
      return;
    }

    this.novel.compendiumIds = this.novel.compendiumIds.includes(compendiumId)
      ? this.novel.compendiumIds.filter((id) => id !== compendiumId)
      : [...this.novel.compendiumIds, compendiumId];

    this.onBlur();
  }
}
