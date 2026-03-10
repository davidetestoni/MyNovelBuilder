import { Component, OnInit, inject } from '@angular/core';
import { RecordOverride } from '../../types/dtos/novel/prose';
import { CompendiumRecordOverviewDto } from '../../types/dtos/compendium-record/compendium-record-overview.dto';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { TooltipModule } from 'primeng/tooltip';
import { CommonModule } from '@angular/common';
import { CompendiumService } from '../../services/compendium.service';
import {
  AutoCompleteCompleteEvent,
  AutoCompleteModule,
} from 'primeng/autocomplete';
import { Prose } from '../../types/dtos/novel/prose';
import { ToastrModule, ToastrService } from 'ngx-toastr';

export interface RecordOverridesEditorComponentData {
  recordOverrides: RecordOverride[];
  availableRecords: CompendiumRecordOverviewDto[];
  prose: Prose;
  chapterIndex: number;
  sectionIndex: number;
}

interface EditableRecordOverride extends RecordOverride {
  isExpanded?: boolean;
  previousDescription?: string;
}

@Component({
  selector: 'app-record-overrides-editor',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    InputTextModule,
    TextareaModule,
    SelectModule,
    TooltipModule,
    AutoCompleteModule,
    ToastrModule,
  ],
  templateUrl: './record-overrides-editor.component.html',
  styleUrl: './record-overrides-editor.component.scss',
})
export class RecordOverridesEditorComponent implements OnInit {
  config = inject(DynamicDialogConfig);
  dialogRef = inject(DynamicDialogRef);
  compendiumService = inject(CompendiumService);
  private toastr = inject(ToastrService);

  overrides: EditableRecordOverride[] = [];
  availableRecords: CompendiumRecordOverviewDto[] = [];
  suggestedKeywords: string[] = [];
  filteredKeywords: string[] = [];
  prose!: Prose;
  chapterIndex!: number;
  sectionIndex!: number;
  private recordContexts = new Map<string, string>();

  ngOnInit(): void {
    const data = this.config.data as RecordOverridesEditorComponentData;
    // Clone the overrides to avoid direct modification before saving
    this.overrides = (data.recordOverrides || []).map((o) => ({
      ...o,
      isExpanded: false,
      previousDescription: '',
    }));
    this.availableRecords = data.availableRecords;
    this.prose = data.prose;
    this.chapterIndex = data.chapterIndex;
    this.sectionIndex = data.sectionIndex;

    for (const override of this.overrides) {
      if (override.compendiumRecordId) {
        this.fetchSuggestedKeywords(override.compendiumRecordId);
      }
    }

    this.refreshPreviousDescriptions();
  }

  addOverride() {
    const compendiumRecordId =
      this.availableRecords.length > 0 ? this.availableRecords[0].id : '';
    this.overrides.push({
      compendiumRecordId: compendiumRecordId,
      keyword: '',
      description: '',
      isExpanded: true,
      previousDescription: '',
    });
    this.fetchSuggestedKeywords(compendiumRecordId);
    this.refreshPreviousDescriptions();
  }

  toggleExpand(index: number) {
    this.overrides[index].isExpanded = !this.overrides[index].isExpanded;
  }

  getRecordName(id: string): string {
    const record = this.availableRecords.find((r) => r.id === id);
    return record ? record.name : 'Unknown Record';
  }

  onRecordChange(override: EditableRecordOverride): void {
    override.keyword = '';
    override.previousDescription = '';
    this.fetchSuggestedKeywords(override.compendiumRecordId);
    this.refreshPreviousDescriptions();
  }

  onKeywordChange(): void {
    this.refreshPreviousDescriptions();
  }

  onDescriptionChange(): void {
    this.refreshPreviousDescriptions();
  }

  fetchSuggestedKeywords(recordId: string): void {
    if (!recordId) {
      this.suggestedKeywords = [];
      this.filteredKeywords = [];
      return;
    }

    const cachedContext = this.recordContexts.get(recordId);
    if (cachedContext !== undefined) {
      this.suggestedKeywords = this.parseKeywords(cachedContext);
      return;
    }

    this.compendiumService.getRecord(recordId).subscribe((record) => {
      this.recordContexts.set(recordId, record.context);
      this.suggestedKeywords = this.parseKeywords(record.context);
      this.refreshPreviousDescriptions();
    });
  }

  private parseKeywords(keywordsStr: string): string[] {
    // Context regions are in the format:
    // [keyword]...[/keyword]
    const regex = /\[([^\]]+)\](?:.|\n)*?\[\/\1\]/g;
    const matches = [];
    let match;
    while ((match = regex.exec(keywordsStr)) !== null) {
      matches.push(match[1]);
    }
    return matches;
  }

  searchKeywords(event: AutoCompleteCompleteEvent) {
    const query = event.query.toLowerCase();
    this.filteredKeywords = this.suggestedKeywords.filter((keyword) =>
      keyword.toLowerCase().includes(query),
    );
  }

  removeOverride(index: number) {
    this.overrides.splice(index, 1);
    this.refreshPreviousDescriptions();
  }

  getPreviousDescriptionMessage(override: EditableRecordOverride): string {
    if (!override.compendiumRecordId) {
      return 'Select a record to view the current value.';
    }

    if (!override.keyword.trim()) {
      return 'Select a keyword to view the current value.';
    }

    if (override.previousDescription?.trim()) {
      return override.previousDescription;
    }

    return 'No existing value found for this keyword.';
  }

  save() {
    const duplicate = this.findDuplicateOverride();
    if (duplicate) {
      this.toastr.error(
        `Duplicate override for "${this.getRecordName(duplicate.compendiumRecordId)}" / "${duplicate.keyword.trim() || '[empty keyword]'}".`,
      );
      return;
    }

    // Remove editor-only state before returning
    const result = this.overrides.map(
      ({ isExpanded, previousDescription, ...rest }) => rest,
    );
    this.dialogRef.close(result);
  }

  cancel() {
    this.dialogRef.close();
  }

  private refreshPreviousDescriptions(): void {
    this.overrides.forEach((override, index) => {
      override.previousDescription = this.getEffectivePreviousDescription(index);
    });
  }

  private getEffectivePreviousDescription(overrideIndex: number): string {
    const override = this.overrides[overrideIndex];
    const normalizedKeyword = override.keyword.trim();

    if (!override.compendiumRecordId || !normalizedKeyword) {
      return '';
    }

    const latestPriorOverride = this.findLatestPriorMatchingOverride(
      override.compendiumRecordId,
      normalizedKeyword,
      overrideIndex,
    );
    if (latestPriorOverride) {
      return latestPriorOverride.description.trim();
    }

    return this.extractOriginalDescription(
      override.compendiumRecordId,
      normalizedKeyword,
    );
  }

  private findLatestPriorMatchingOverride(
    recordId: string,
    keyword: string,
    excludedOverrideIndex: number,
  ): RecordOverride | null {
    const normalizedKeyword = keyword.trim().toLowerCase();
    let latestMatch: RecordOverride | null = null;

    for (let c = 0; c <= this.chapterIndex; c++) {
      const sections = this.prose.chapters[c]?.sections ?? [];
      const lastSectionIndex =
        c === this.chapterIndex ? this.sectionIndex : sections.length - 1;

      for (let s = 0; s <= lastSectionIndex; s++) {
        const sectionOverrides =
          c === this.chapterIndex && s === this.sectionIndex
            ? this.overrides.slice(0, excludedOverrideIndex)
            : (sections[s]?.recordOverrides ?? []);

        for (const override of sectionOverrides) {
          if (override.compendiumRecordId !== recordId) {
            continue;
          }

          if (override.keyword.trim().toLowerCase() !== normalizedKeyword) {
            continue;
          }

          latestMatch = override;
        }
      }
    }

    return latestMatch;
  }

  private extractOriginalDescription(recordId: string, keyword: string): string {
    const context = this.recordContexts.get(recordId) ?? '';
    if (!context) {
      return '';
    }

    const escapedKeyword = keyword.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const regex = new RegExp(
      `\\[${escapedKeyword}\\]([\\s\\S]*?)\\[\\/${escapedKeyword}\\]`,
      'i',
    );
    const match = context.match(regex);

    return match?.[1]?.trim() ?? '';
  }

  private findDuplicateOverride(): RecordOverride | null {
    const seen = new Set<string>();

    for (const override of this.overrides) {
      const recordId = override.compendiumRecordId.trim();
      const keyword = override.keyword.trim().toLowerCase();

      if (!recordId || !keyword) {
        continue;
      }

      const key = `${recordId}::${keyword}`;
      if (seen.has(key)) {
        return override;
      }

      seen.add(key);
    }

    return null;
  }
}
