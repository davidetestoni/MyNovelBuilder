import { Component, OnDestroy, inject } from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
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
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { FileUploadModule } from 'primeng/fileupload';
import { MultiSelectModule } from 'primeng/multiselect';
import { MenuModule } from 'primeng/menu';
import { ConfirmationService, MenuItem } from 'primeng/api';
import { firstValueFrom } from 'rxjs';
import { getFileNameFromResponse } from '../../utils/http.utils';
import { NovelExportFormat } from '../../services/novel.service';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { PromptService } from '../../services/prompt.service';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { PromptType } from '../../types/enums/prompt-type';
import {
  TranslateNovelDialogComponent,
  TranslateNovelDialogResult,
} from '../../components/translate-novel-dialog/translate-novel-dialog.component';
import { ConfirmDialogModule } from 'primeng/confirmdialog';

@Component({
  selector: 'app-novel-settings',
  standalone: true,
  imports: [
    FormsModule,
    RouterModule,
    SpacedPipe,
    ButtonModule,
    InputTextModule,
    TextareaModule,
    SelectModule,
    FileUploadModule,
    MultiSelectModule,
    MenuModule,
    ConfirmDialogModule,
  ],
  providers: [DialogService, ConfirmationService],
  templateUrl: './novel-settings.component.html',
  styleUrl: './novel-settings.component.scss',
})
export class NovelSettingsComponent implements OnDestroy {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private dialogService = inject(DialogService);
  private confirmationService = inject(ConfirmationService);
  private dialogRef: DynamicDialogRef | null = null;

  novel: NovelDto | null = null;
  compendia: CompendiumDto[] | null = null;
  readonly novelService: NovelService = inject(NovelService);
  readonly compendiumService: CompendiumService = inject(CompendiumService);
  readonly promptService: PromptService = inject(PromptService);
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

  exportFormatMenuItems: MenuItem[] = [
    {
      label: 'Markdown',
      icon: 'pi pi-file-edit',
      command: () => void this.exportNovel('markdown'),
    },
    {
      label: 'HTML',
      icon: 'pi pi-code',
      command: () => void this.exportNovel('html'),
    },
    {
      label: 'PDF',
      icon: 'pi pi-file-pdf',
      command: () => void this.exportNovel('pdf'),
    },
  ];

  ngOnInit(): void {
    this.novelId = this.route.snapshot.paramMap.get('id')!;
    this.getNovel();
    this.getCompendia();
  }

  ngOnDestroy(): void {
    this.dialogRef?.close();
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

  async goToProse(): Promise<void> {
    await this.router.navigate(['/novel', this.novelId]);
  }

  onBlur() {
    if (this.novel === null) {
      return;
    }

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

  updateNovelCoverImage(event: Event) {
    if (this.novel === null) {
      return;
    }

    const target = event.target as HTMLInputElement;

    if (
      target.files !== null &&
      target.files !== undefined &&
      target.files.length > 0
    ) {
      this.novelService
        .uploadNovelCoverImage(this.novel.id, target.files[0])
        .subscribe(() => {
          this.getNovel();
        });
    }
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

  async exportNovel(format: NovelExportFormat): Promise<void> {
    if (this.novel === null) {
      return;
    }

    const response = await firstValueFrom(
      this.novelService.exportNovel(this.novel.id, format),
    );
    const blob = response.body;
    if (!blob) {
      return;
    }

    const fileName = getFileNameFromResponse(
      response,
      `${this.novel.id}.${format === 'markdown' ? 'md' : format}`,
    );
    this.downloadBlob(blob, fileName);
  }

  async openTranslateDialog(): Promise<void> {
    if (this.novel === null) {
      return;
    }

    const [prose, prompts] = await Promise.all([
      firstValueFrom(this.novelService.getNovelProse(this.novel.id)),
      firstValueFrom(this.promptService.getPrompts()),
    ]);

    this.dialogRef = this.dialogService.open(TranslateNovelDialogComponent, {
      header: 'Translate novel',
      width: '52rem',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      modal: true,
      closable: true,
      closeOnEscape: true,
      dismissableMask: true,
      data: {
        novel: this.novel,
        prose,
        prompts: prompts.filter(
          (prompt: PromptDto) => prompt.type === PromptType.TranslateNovel,
        ),
      },
    });

    this.dialogRef?.onClose.subscribe((result: TranslateNovelDialogResult | undefined) => {
      if (!result) {
        return;
      }

      void this.router.navigate(['/novel', result.novelId]);
    });
  }

  confirmDeleteNovel(): void {
    if (this.novel === null) {
      return;
    }

    this.confirmationService.confirm({
      message:
        'Are you sure you want to delete this novel? This action cannot be undone.',
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.novelService.deleteNovel(this.novel!.id).subscribe(() => {
          void this.router.navigate(['/novels']);
        });
      },
    });
  }

  private downloadBlob(blob: Blob, fileName: string): void {
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    window.URL.revokeObjectURL(url);
  }
}
