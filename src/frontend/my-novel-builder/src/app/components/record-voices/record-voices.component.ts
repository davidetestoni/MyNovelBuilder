import { Component, Input, OnChanges, OnDestroy, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { IntegrationsService } from '../../services/integrations.service';
import { GenerateAudioService } from '../../services/generate-audio.service';
import { CharacterVoiceAssignmentDto } from '../../types/dtos/compendium-record/character-voice-assignment.dto';
import { SpacedPipe } from '../../pipes/spaced.pipe';
import { STREAMING_WAV_PLAYER_FACTORY, StreamingWavPlayerHandle } from '../../utils/streaming-wav-player.factory';
import { getVoicePreviewText, getVoicePreviewLanguage } from '../../utils/tts-preview.utils';

@Component({
  selector: 'app-record-voices',
  standalone: true,
  imports: [SpacedPipe],
  templateUrl: './record-voices.component.html',
  styleUrl: './record-voices.component.scss',
})
export class RecordVoicesComponent implements OnChanges, OnDestroy {
  @Input() assignments: CharacterVoiceAssignmentDto[] = [];
  private readonly integrations = inject(IntegrationsService);
  private readonly audio = inject(GenerateAudioService);
  private readonly createPlayer = inject(STREAMING_WAV_PLAYER_FACTORY);
  private abort?: AbortController;
  private reader?: ReadableStreamDefaultReader<Uint8Array>;
  private player?: StreamingWavPlayerHandle;
  activeAssignment: CharacterVoiceAssignmentDto | null = null;
  loading = false;
  error: string | null = null;

  ngOnChanges(): void {
    this.stop();
    this.error = null;
  }

  ngOnDestroy(): void {
    this.stop();
  }

  async preview(assignment: CharacterVoiceAssignmentDto): Promise<void> {
    const wasActive = this.activeAssignment === assignment;
    this.stop();
    this.error = null;
    if (wasActive) return;

    const abort = new AbortController();
    this.abort = abort;
    this.activeAssignment = assignment;
    this.loading = true;
    let reader: ReadableStreamDefaultReader<Uint8Array> | undefined;
    try {
      const [models, config] = await Promise.all([
        firstValueFrom(this.audio.getAvailableModels(assignment.provider)).catch(() => []),
        firstValueFrom(this.integrations.getIntegrationsConfig()),
      ]);
      if (abort.signal.aborted) return;
      const language = getVoicePreviewLanguage(
        models.find(model => model.modelId === assignment.modelId)
          ?.voices.find(voice => voice.voiceId === assignment.voiceId),
        config.ttsPreviewLanguage,
      );
      const response = await this.audio.textToSpeechStreamResponse({
        provider: assignment.provider,
        modelId: assignment.modelId,
        voiceId: assignment.voiceId,
        gainDb: assignment.gainDb ?? 0,
        message: getVoicePreviewText(language),
      }, abort.signal);
      if (abort.signal.aborted) {
        await response.body?.cancel();
        return;
      }
      if (!response.ok || !response.body) throw new Error('No voice sample returned');
      const player = this.createPlayer();
      this.player = player;
      reader = response.body.getReader();
      this.reader = reader;
      while (!abort.signal.aborted) {
        const { done, value } = await reader.read();
        if (done) break;
        if (value && !abort.signal.aborted) player.addChunk(value);
      }
    } catch {
      if (!abort.signal.aborted) {
        this.stop();
        this.error = 'Could not preview voice. Please verify your TTS configuration.';
      }
    } finally {
      reader?.releaseLock();
      if (this.abort === abort) {
        this.loading = false;
        this.reader = undefined;
      }
    }
  }

  stop(): void {
    this.abort?.abort();
    void this.reader?.cancel().catch(() => {});
    this.player?.stop();
    this.abort = undefined;
    this.reader = undefined;
    this.player = undefined;
    this.activeAssignment = null;
    this.loading = false;
  }
}
