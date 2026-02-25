import { Component, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { ButtonModule } from 'primeng/button';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { VoiceService } from '../../services/voice.service';
import { VoiceDto } from '../../types/dtos/voice/voice.dto';
import { VoiceGender } from '../../types/enums/voice-gender';

export interface VoiceDialogData {
  mode: 'create' | 'edit';
  voice?: VoiceDto;
}

@Component({
  selector: 'app-voice-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, InputTextModule, SelectModule, ButtonModule],
  templateUrl: './voice-dialog.component.html',
  styleUrl: './voice-dialog.component.scss',
})
export class VoiceDialogComponent {
  private dialogRef = inject(DynamicDialogRef);
  private config = inject(DynamicDialogConfig);
  private voiceService = inject(VoiceService);
  private toastr = inject(ToastrService);

  protected readonly data = (this.config.data || { mode: 'create' }) as VoiceDialogData;
  protected selectedFileName = '';

  protected readonly voiceGenderOptions = [
    { label: 'Both', value: VoiceGender.Both },
    { label: 'Male', value: VoiceGender.Male },
    { label: 'Female', value: VoiceGender.Female },
  ];

  protected readonly formGroup = new FormGroup({
    name: new FormControl('', [Validators.required, Validators.maxLength(100)]),
    voiceGender: new FormControl<VoiceGender>(VoiceGender.Both, [Validators.required]),
    file: new FormControl<File | null>(null, [Validators.required]),
  });

  constructor() {
    if (this.data.mode === 'edit' && this.data.voice) {
      this.formGroup.patchValue({
        name: this.data.voice.name,
        voiceGender: this.data.voice.voiceGender,
      });
    }
  }

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;

    if (file === null) {
      this.formGroup.controls.file.setValue(null);
      this.selectedFileName = '';
      return;
    }

    if (!file.name.toLowerCase().endsWith('.wav')) {
      this.formGroup.controls.file.setValue(null);
      input.value = '';
      this.selectedFileName = '';
      this.toastr.error('Only .wav files are allowed.');
      return;
    }

    this.formGroup.controls.file.setValue(file);
    this.selectedFileName = file.name;
    this.formGroup.controls.file.markAsDirty();
  }

  protected submit(): void {
    if (this.formGroup.invalid) {
      return;
    }

    const name = this.formGroup.controls.name.value?.trim() ?? '';
    const voiceGender = this.formGroup.controls.voiceGender.value ?? VoiceGender.Both;
    const file = this.formGroup.controls.file.value;

    if (!name || file === null) {
      return;
    }

    if (this.data.mode === 'edit' && this.data.voice) {
      this.voiceService
        .updateVoice(this.data.voice.id, name, voiceGender, file)
        .subscribe(() => {
          this.toastr.success('Voice updated successfully.');
          this.dialogRef.close(true);
        });
      return;
    }

    this.voiceService.createVoice(name, voiceGender, file).subscribe(() => {
      this.toastr.success('Voice created successfully.');
      this.dialogRef.close(true);
    });
  }

  protected cancel(): void {
    this.dialogRef.close();
  }
}
