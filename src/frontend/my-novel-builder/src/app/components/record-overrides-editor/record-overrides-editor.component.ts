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

export interface RecordOverridesEditorComponentData {
  recordOverrides: RecordOverride[];
  availableRecords: CompendiumRecordOverviewDto[];
}

interface EditableRecordOverride extends RecordOverride {
  isExpanded?: boolean;
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
  ],
  templateUrl: './record-overrides-editor.component.html',
  styleUrl: './record-overrides-editor.component.scss',
})
export class RecordOverridesEditorComponent implements OnInit {
  config = inject(DynamicDialogConfig);
  dialogRef = inject(DynamicDialogRef);
  compendiumService = inject(CompendiumService);

  overrides: EditableRecordOverride[] = [];
  availableRecords: CompendiumRecordOverviewDto[] = [];
  suggestedKeywords: string[] = [];
  filteredKeywords: string[] = [];

  ngOnInit(): void {
    const data = this.config.data as RecordOverridesEditorComponentData;
    // Clone the overrides to avoid direct modification before saving
    this.overrides = (data.recordOverrides || []).map((o) => ({
      ...o,
      isExpanded: false,
    }));
    this.availableRecords = data.availableRecords;
  }

  addOverride() {
    const compendiumRecordId =
      this.availableRecords.length > 0 ? this.availableRecords[0].id : '';
    this.overrides.push({
      compendiumRecordId: compendiumRecordId,
      keyword: '',
      description: '',
      isExpanded: true,
    });
    this.fetchSuggestedKeywords(compendiumRecordId);
  }

  toggleExpand(index: number) {
    this.overrides[index].isExpanded = !this.overrides[index].isExpanded;
  }

  getRecordName(id: string): string {
    const record = this.availableRecords.find((r) => r.id === id);
    return record ? record.name : 'Unknown Record';
  }

  fetchSuggestedKeywords(recordId: string): void {
    this.compendiumService.getRecord(recordId).subscribe((record) => {
      this.suggestedKeywords = this.parseKeywords(record.context);
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
  }

  save() {
    // Remove isExpanded before returning
    const result = this.overrides.map(({ isExpanded, ...rest }) => rest);
    this.dialogRef.close(result);
  }

  cancel() {
    this.dialogRef.close();
  }
}
