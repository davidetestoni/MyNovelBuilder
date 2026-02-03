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
  ],
  templateUrl: './record-overrides-editor.component.html',
  styleUrl: './record-overrides-editor.component.scss',
})
export class RecordOverridesEditorComponent implements OnInit {
  config = inject(DynamicDialogConfig);
  dialogRef = inject(DynamicDialogRef);

  overrides: EditableRecordOverride[] = [];
  availableRecords: CompendiumRecordOverviewDto[] = [];

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
    this.overrides.push({
      compendiumRecordId:
        this.availableRecords.length > 0 ? this.availableRecords[0].id : '',
      keyword: '',
      description: '',
      isExpanded: true,
    });
  }

  toggleExpand(index: number) {
    this.overrides[index].isExpanded = !this.overrides[index].isExpanded;
  }

  getRecordName(id: string): string {
    const record = this.availableRecords.find((r) => r.id === id);
    return record ? record.name : 'Unknown Record';
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
