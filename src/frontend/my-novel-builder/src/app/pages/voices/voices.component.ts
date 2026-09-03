import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { TitleCasePipe } from '@angular/common';
import { ToastrService } from 'ngx-toastr';
import { ConfirmationService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { VoiceDialogComponent } from '../../components/voice-dialog/voice-dialog.component';
import { VoiceService } from '../../services/voice.service';
import { VoiceDto } from '../../types/dtos/voice/voice.dto';
import { VoiceGender } from '../../types/enums/voice-gender';
import {
  STREAMING_WAV_PLAYER_FACTORY,
  StreamingWavPlayerHandle,
} from '../../utils/streaming-wav-player.factory';

@Component({
  selector: 'app-voices',
  standalone: true,
  imports: [ButtonModule, ConfirmDialogModule, TitleCasePipe],
  templateUrl: './voices.component.html',
  styleUrl: './voices.component.scss',
  providers: [DialogService, ConfirmationService],
})
export class VoicesComponent implements OnInit, OnDestroy {
  voices: VoiceDto[] | null = null;
  previewingVoiceId: string | null = null;

  private voiceService = inject(VoiceService);
  private toastrService = inject(ToastrService);
  private dialogService = inject(DialogService);
  private confirmationService = inject(ConfirmationService);
  private createStreamingWavPlayer = inject(STREAMING_WAV_PLAYER_FACTORY);
  private previewPlayer: StreamingWavPlayerHandle | null = null;
  private dialogRef: DynamicDialogRef | null = null;

  ngOnInit(): void {
    this.loadVoices();
  }

  ngOnDestroy(): void {
    this.previewPlayer?.stop();
    this.dialogRef?.close();
  }

  getGenderLabel(voiceGender: VoiceGender): string {
    switch (voiceGender) {
      case VoiceGender.Male:
        return 'Male';
      case VoiceGender.Female:
        return 'Female';
      case VoiceGender.Both:
      default:
        return 'Both';
    }
  }

  async previewVoice(voiceId: string): Promise<void> {
    if (this.previewingVoiceId !== null) {
      return;
    }

    this.previewingVoiceId = voiceId;
    this.previewPlayer?.stop();
    this.previewPlayer = this.createStreamingWavPlayer();

    try {
      const response = await this.voiceService.getVoicePreviewStreamResponse(
        voiceId,
        3,
      );
      const stream = response.body;

      if (!stream) {
        this.toastrService.error('No audio stream was returned.');
        return;
      }

      const reader = stream.getReader();

      while (true) {
        const { done, value } = await reader.read();
        if (done) {
          break;
        }

        if (value) {
          this.previewPlayer.addChunk(value);
        }
      }
    } catch (error) {
      console.error('Voice preview streaming error:', error);
      this.toastrService.error('Could not preview voice.');
    } finally {
      this.previewingVoiceId = null;
    }
  }

  openCreateVoiceDialog(): void {
    this.dialogRef = this.dialogService.open(VoiceDialogComponent, {
      header: 'Create Voice',
      width: '35rem',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      modal: true,
      closable: true,
      closeOnEscape: true,
      dismissableMask: true,
      data: { mode: 'create' },
    });

    this.dialogRef?.onClose.subscribe((result) => {
      if (result) {
        this.loadVoices();
      }
    });
  }

  openEditVoiceDialog(voice: VoiceDto, event: Event): void {
    event.stopPropagation();
    event.preventDefault();

    this.dialogRef = this.dialogService.open(VoiceDialogComponent, {
      header: 'Edit Voice',
      width: '35rem',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      modal: true,
      closable: true,
      closeOnEscape: true,
      dismissableMask: true,
      data: { mode: 'edit', voice },
    });

    this.dialogRef?.onClose.subscribe((result) => {
      if (result) {
        this.loadVoices();
      }
    });
  }

  deleteVoice(voice: VoiceDto, event: Event): void {
    event.stopPropagation();
    event.preventDefault();

    this.confirmationService.confirm({
      message: 'Are you sure you want to delete this voice? This action cannot be undone.',
      header: 'Confirm Voice Deletion',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.voiceService.deleteVoice(voice.id).subscribe(() => {
          this.toastrService.success('Voice deleted.');
          this.loadVoices();
        });
      },
    });
  }

  private loadVoices(): void {
    this.voiceService.getVoices().subscribe((voices) => {
      this.voices = voices;
    });
  }
}
